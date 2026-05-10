using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

public class AuditService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    private readonly CurrentUserService _user;

    public AuditService(IDbContextFactory<SalesDbContext> factory, CurrentUserService user)
    {
        _factory = factory;
        _user = user;
    }

    public async Task LogAsync(string action, string entityType, int entityId, string? details = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            UserName = _user.UserName,
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetRecentAsync(int take = 200)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> SearchAsync(
        string? action = null, string? entityType = null, string? user = null,
        DateTime? from = null, DateTime? to = null, int take = 500)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.AuditLogs.AsQueryable();
        if (!string.IsNullOrEmpty(action))     q = q.Where(a => a.Action == action);
        if (!string.IsNullOrEmpty(entityType)) q = q.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrEmpty(user))       q = q.Where(a => a.UserName == user);
        if (from.HasValue)                     q = q.Where(a => a.Timestamp >= from.Value);
        if (to.HasValue)                       q = q.Where(a => a.Timestamp < to.Value.AddDays(1));
        return await q.OrderByDescending(a => a.Timestamp).Take(take).ToListAsync();
    }

    public record PagedAudit(List<AuditLog> Items, int TotalCount);

    public async Task<PagedAudit> SearchPagedAsync(
        string? action, string? entityType, string? user,
        DateTime? from, DateTime? to, int page, int pageSize)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.AuditLogs.AsQueryable();
        if (!string.IsNullOrEmpty(action))     q = q.Where(a => a.Action == action);
        if (!string.IsNullOrEmpty(entityType)) q = q.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrEmpty(user))       q = q.Where(a => a.UserName == user);
        if (from.HasValue)                     q = q.Where(a => a.Timestamp >= from.Value);
        if (to.HasValue)                       q = q.Where(a => a.Timestamp < to.Value.AddDays(1));

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(a => a.Timestamp)
            .Skip(Math.Max(0, (page - 1) * pageSize))
            .Take(pageSize)
            .ToListAsync();
        return new PagedAudit(items, total);
    }

    public async Task<List<string>> GetDistinctActionsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.AuditLogs.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();
    }

    public async Task<List<string>> GetDistinctEntityTypesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.AuditLogs.Select(a => a.EntityType).Distinct().OrderBy(a => a).ToListAsync();
    }

    public async Task<List<string>> GetDistinctUsersAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.AuditLogs.Select(a => a.UserName).Distinct().OrderBy(a => a).ToListAsync();
    }

    public async Task<int> PurgeOlderThanAsync(int days)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var old = db.AuditLogs.Where(a => a.Timestamp < cutoff);
        var count = await old.CountAsync();
        db.AuditLogs.RemoveRange(old);
        await db.SaveChangesAsync();
        return count;
    }
}
