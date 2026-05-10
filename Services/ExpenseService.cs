using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

public record ProfitLoss(decimal Revenue, decimal CostOfGoods, decimal GrossProfit, decimal Expenses, decimal NetProfit);

public class ExpenseService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    private readonly AuditService _audit;

    public ExpenseService(IDbContextFactory<SalesDbContext> factory, AuditService audit)
    { _factory = factory; _audit = audit; }

    public async Task<List<Expense>> GetAllAsync(DateTime? from = null, DateTime? to = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.Expenses.Include(e => e.Store).AsQueryable();
        if (from.HasValue) q = q.Where(e => e.Date >= from.Value);
        if (to.HasValue)   q = q.Where(e => e.Date < to.Value.AddDays(1));
        return await q.OrderByDescending(e => e.Date).ToListAsync();
    }

    public async Task<PagedResult<Expense>> GetPagedAsync(int page, int pageSize, DateTime? from = null, DateTime? to = null, string? category = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.Expenses.Include(e => e.Store).AsQueryable();
        if (from.HasValue) q = q.Where(e => e.Date >= from.Value);
        if (to.HasValue)   q = q.Where(e => e.Date < to.Value.AddDays(1));
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(e => e.Category == category);
        return await q.OrderByDescending(e => e.Date).ToPagedAsync(page, pageSize);
    }

    public async Task<int> SaveAsync(Expense e)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (e.Id == 0) db.Expenses.Add(e); else db.Expenses.Update(e);
        await db.SaveChangesAsync();
        await _audit.LogAsync(e.Id == 0 ? "Created" : "Updated", "Expense", e.Id, $"{e.Category}: {e.Amount:N2}");
        return e.Id;
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var e = await db.Expenses.FindAsync(id);
        if (e != null) { db.Expenses.Remove(e); await db.SaveChangesAsync(); await _audit.LogAsync("Deleted", "Expense", id); }
    }

    public async Task<ProfitLoss> GetProfitLossAsync(DateTime from, DateTime to)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var sales = db.Sales.Include(s => s.Items).Where(s => s.Status == SaleStatus.Completed && s.SaleDate >= from && s.SaleDate < to.AddDays(1));
        var revenue = await sales.SumAsync(s => (decimal?)s.Subtotal) ?? 0m;
        var allItems = await sales.SelectMany(s => s.Items).ToListAsync();
        var cogs = allItems.Sum(i => i.CostPrice * i.Quantity);
        var expenses = await db.Expenses.Where(e => e.Date >= from && e.Date < to.AddDays(1)).SumAsync(e => (decimal?)e.Amount) ?? 0m;
        var gross = revenue - cogs;
        return new ProfitLoss(revenue, cogs, gross, expenses, gross - expenses);
    }
}
