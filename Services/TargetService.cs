using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

public record TargetProgress(string Scope, int? StoreId, decimal Target, decimal Actual, decimal Percent);

public class TargetService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    public TargetService(IDbContextFactory<SalesDbContext> factory) => _factory = factory;

    public async Task<List<SalesTarget>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.SalesTargets.Include(t => t.Store)
            .OrderByDescending(t => t.Year).ThenByDescending(t => t.Month).ToListAsync();
    }

    public async Task SaveAsync(SalesTarget target)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (target.Id == 0) db.SalesTargets.Add(target); else db.SalesTargets.Update(target);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var t = await db.SalesTargets.FindAsync(id);
        if (t != null) { db.SalesTargets.Remove(t); await db.SaveChangesAsync(); }
    }

    public async Task<List<TargetProgress>> GetProgressAsync(int year, int month)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var sales = db.Sales.Where(s => s.Status == SaleStatus.Completed
            && s.SaleDate >= monthStart && s.SaleDate < monthEnd);

        var byStore = await sales.GroupBy(s => s.StoreId)
            .Select(g => new { StoreId = g.Key, Total = g.Sum(x => x.Total) })
            .ToListAsync();
        var company = byStore.Sum(x => x.Total);

        var targets = await db.SalesTargets
            .Include(t => t.Store)
            .Where(t => t.Year == year && t.Month == month).ToListAsync();

        var list = new List<TargetProgress>();
        foreach (var t in targets)
        {
            decimal actual = t.StoreId.HasValue
                ? byStore.FirstOrDefault(x => x.StoreId == t.StoreId)?.Total ?? 0
                : company;
            decimal pct = t.TargetAmount == 0 ? 0 : Math.Round(actual / t.TargetAmount * 100m, 1);
            list.Add(new TargetProgress(
                t.StoreId.HasValue ? t.Store?.Name ?? $"Store #{t.StoreId}" : "Company-wide",
                t.StoreId, t.TargetAmount, actual, pct));
        }
        return list;
    }
}
