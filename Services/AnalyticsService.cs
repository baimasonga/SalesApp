using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

public record DashboardStats(
    decimal RevenueToday,
    decimal RevenueMonth,
    int OrdersToday,
    int OrdersMonth,
    int CustomerCount,
    int LowStockCount,
    decimal AverageOrderValue);

public record SeriesPoint(string Label, decimal Value);

public class AnalyticsService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    public AnalyticsService(IDbContextFactory<SalesDbContext> factory) => _factory = factory;

    public async Task<DashboardStats> GetStatsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var sales = db.Sales.Where(s => s.Status == SaleStatus.Completed);

        var revenueToday = await sales.Where(s => s.SaleDate >= today).SumAsync(s => (decimal?)s.Total) ?? 0;
        var revenueMonth = await sales.Where(s => s.SaleDate >= monthStart).SumAsync(s => (decimal?)s.Total) ?? 0;
        var ordersToday = await sales.CountAsync(s => s.SaleDate >= today);
        var ordersMonth = await sales.CountAsync(s => s.SaleDate >= monthStart);
        var customerCount = await db.Customers.CountAsync();
        var lowStock = await db.InventoryItems.CountAsync(i => i.QuantityOnHand <= i.ReorderLevel);
        var aov = ordersMonth == 0 ? 0 : Math.Round(revenueMonth / ordersMonth, 2);

        return new DashboardStats(revenueToday, revenueMonth, ordersToday, ordersMonth, customerCount, lowStock, aov);
    }

    public async Task<List<SeriesPoint>> GetDailyRevenueAsync(int days = 14)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var start = DateTime.UtcNow.Date.AddDays(-days + 1);
        var rows = await db.Sales
            .Where(s => s.Status == SaleStatus.Completed && s.SaleDate >= start)
            .GroupBy(s => s.SaleDate.Date)
            .Select(g => new { Date = g.Key, Total = g.Sum(x => x.Total) })
            .ToListAsync();

        var map = rows.ToDictionary(r => r.Date, r => r.Total);
        var points = new List<SeriesPoint>();
        for (int i = 0; i < days; i++)
        {
            var d = start.AddDays(i);
            points.Add(new SeriesPoint(d.ToString("MMM d"), map.TryGetValue(d, out var v) ? v : 0));
        }
        return points;
    }

    public async Task<List<SeriesPoint>> GetTopProductsAsync(int top = 5)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.SaleItems
            .Where(i => i.Sale!.Status == SaleStatus.Completed)
            .GroupBy(i => i.ProductName)
            .Select(g => new { Label = g.Key, Total = g.Sum(x => x.LineTotal) })
            .ToListAsync();
        return rows.OrderByDescending(r => r.Total).Take(top)
            .Select(r => new SeriesPoint(r.Label, r.Total)).ToList();
    }

    public async Task<List<SeriesPoint>> GetSalesByStoreAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Sales
            .Where(s => s.Status == SaleStatus.Completed)
            .GroupBy(s => s.Store!.Name)
            .Select(g => new { Label = g.Key, Total = g.Sum(x => x.Total) })
            .ToListAsync();
        return rows.OrderByDescending(r => r.Total)
            .Select(r => new SeriesPoint(r.Label, r.Total)).ToList();
    }

    public async Task<List<SeriesPoint>> GetPaymentMixAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Sales
            .Where(s => s.Status == SaleStatus.Completed)
            .GroupBy(s => s.PaymentMethod)
            .Select(g => new { Method = g.Key, Total = g.Sum(x => x.Total) })
            .ToListAsync();
        return rows.Select(r => new SeriesPoint(r.Method.ToString(), r.Total)).ToList();
    }
}
