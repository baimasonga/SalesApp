using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SalesApp.Models;
using SalesApp.Services;

namespace SalesApp.Data;

/// <summary>
/// Captures every EF SaveChanges into the AuditLog table — automatically.
/// This guarantees every Added / Modified / Deleted entity is recorded
/// without having to remember to call AuditService manually.
///
/// Strategy:
///   1. SavingChangesAsync — capture entries (states, original/current values)
///      because original values are lost after save.
///   2. SavedChangesAsync   — entity IDs are now assigned for inserts; build
///      the AuditLog rows and persist them via the same context, guarded by
///      a flag to avoid recursing into ourselves.
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly CurrentUserService _user;
    private static readonly AsyncLocal<bool> _suppress = new();
    private List<PendingAudit>? _pending;

    /// <summary>Entities to NEVER audit (avoids infinite recursion + noise).</summary>
    private static readonly HashSet<string> Skip = new()
    {
        nameof(AuditLog),
    };

    /// <summary>Properties we don't bother diffing (auto-stamps, FK shadow keys we already capture).</summary>
    private static readonly HashSet<string> SkipProps = new()
    {
        "UpdatedAt", "CreatedAt"
    };

    public AuditInterceptor(CurrentUserService user) { _user = user; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (_suppress.Value || eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, ct);

        _pending = Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
    {
        if (_suppress.Value || _pending is null || _pending.Count == 0 || eventData.Context is null)
            return await base.SavedChangesAsync(eventData, result, ct);

        var ctx = eventData.Context;
        try
        {
            _suppress.Value = true;
            foreach (var p in _pending)
            {
                int id = 0;
                try
                {
                    var pkVal = ctx.Entry(p.Entity).Properties
                        .FirstOrDefault(x => x.Metadata.IsPrimaryKey())?.CurrentValue;
                    if (pkVal is int i) id = i;
                }
                catch { /* entity was deleted; id from snapshot already captured */ id = p.PrimaryKey; }

                ctx.Add(new AuditLog
                {
                    Action = p.Action,
                    EntityType = p.EntityType,
                    EntityId = id != 0 ? id : p.PrimaryKey,
                    UserName = _user.UserName ?? "System",
                    Details = p.Details,
                    Timestamp = DateTime.UtcNow,
                });
            }
            await ctx.SaveChangesAsync(ct);
        }
        finally
        {
            _suppress.Value = false;
            _pending = null;
        }
        return await base.SavedChangesAsync(eventData, result, ct);
    }

    private static List<PendingAudit> Capture(DbContext ctx)
    {
        var list = new List<PendingAudit>();
        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            var typeName = entry.Entity.GetType().Name;
            if (Skip.Contains(typeName)) continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            var action = entry.State switch
            {
                EntityState.Added    => "Created",
                EntityState.Modified => "Updated",
                EntityState.Deleted  => "Deleted",
                _                    => entry.State.ToString()
            };

            int pk = 0;
            var pkProp = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
            if (pkProp?.CurrentValue is int pki) pk = pki;
            else if (pkProp?.OriginalValue is int pko) pk = pko;

            string? details = action switch
            {
                "Updated" => DiffModified(entry),
                "Created" => SnapshotImportant(entry),
                "Deleted" => SnapshotImportant(entry, useOriginal: true),
                _ => null
            };

            list.Add(new PendingAudit(entry.Entity, typeName, action, pk, details));
        }
        return list;
    }

    private static string? DiffModified(EntityEntry entry)
    {
        var changed = entry.Properties
            .Where(p => p.IsModified
                        && !p.Metadata.IsPrimaryKey()
                        && !SkipProps.Contains(p.Metadata.Name))
            .Select(p => $"{p.Metadata.Name}: {Trim(p.OriginalValue)} → {Trim(p.CurrentValue)}")
            .ToList();
        if (changed.Count == 0) return null;
        return Truncate(string.Join("; ", changed), 480);
    }

    /// <summary>Snapshot a few human-meaningful fields (Name/Number/Total/etc.) for create/delete logs.</summary>
    private static string? SnapshotImportant(EntityEntry entry, bool useOriginal = false)
    {
        var prefer = new[]
        {
            "InvoiceNumber", "PoNumber", "TransferNumber", "QuoteNumber", "LayawayNumber",
            "Sku", "FullName", "Name", "BusinessName", "Description", "Subject",
            "Total", "Amount", "TargetAmount", "QuantityOnHand"
        };
        var bits = new List<string>();
        foreach (var name in prefer)
        {
            var prop = entry.Properties.FirstOrDefault(p => p.Metadata.Name == name);
            if (prop is null) continue;
            var val = useOriginal ? prop.OriginalValue : prop.CurrentValue;
            if (val is null) continue;
            var s = Trim(val);
            if (string.IsNullOrWhiteSpace(s)) continue;
            bits.Add($"{name}={s}");
            if (bits.Count >= 3) break;
        }
        return bits.Count == 0 ? null : Truncate(string.Join(", ", bits), 480);
    }

    private static string Trim(object? v)
    {
        if (v is null) return "(null)";
        var s = v.ToString() ?? "";
        return s.Length <= 50 ? s : s[..50] + "…";
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    private record PendingAudit(object Entity, string EntityType, string Action, int PrimaryKey, string? Details);
}
