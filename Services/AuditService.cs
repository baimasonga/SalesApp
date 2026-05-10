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
}
