using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

public class SalesService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    private readonly AuditService _audit;
    private readonly CurrentUserService _user;
    private readonly SettingsService _settings;

    public SalesService(IDbContextFactory<SalesDbContext> factory, AuditService audit, CurrentUserService user, SettingsService settings)
    {
        _factory = factory;
        _audit = audit;
        _user = user;
        _settings = settings;
    }

    public async Task<List<Sale>> GetRecentAsync(int take = 50, DateTime? from = null, DateTime? to = null, int? storeId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.Sales
            .Include(s => s.Customer)
            .Include(s => s.Store)
            .Include(s => s.Items)
            .AsQueryable();
        if (from.HasValue) q = q.Where(s => s.SaleDate >= from.Value);
        if (to.HasValue) q = q.Where(s => s.SaleDate < to.Value.AddDays(1));
        if (storeId.HasValue) q = q.Where(s => s.StoreId == storeId.Value);
        return await q.OrderByDescending(s => s.SaleDate).Take(take).ToListAsync();
    }

    public async Task<Sale?> GetAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Sales
            .Include(s => s.Customer)
            .Include(s => s.Store)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<int> CreateAsync(Sale sale)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var gstRate = await _settings.GetAsync(SettingKeys.GstRate, 0.15m);
        var loyaltyPer = await _settings.GetAsync(SettingKeys.LoyaltyPerCurrency, 100m);

        // Recompute totals server-side
        foreach (var i in sale.Items)
        {
            i.LineTotal = (i.Quantity * i.UnitPrice) - i.LineDiscount;
        }
        sale.Subtotal = sale.Items.Sum(i => i.LineTotal);
        sale.Tax = Math.Round(sale.Subtotal * gstRate, 2);
        sale.Total = sale.Subtotal + sale.Tax - sale.Discount;
        if (string.IsNullOrWhiteSpace(sale.InvoiceNumber))
            sale.InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}";
        if (string.IsNullOrWhiteSpace(sale.CashierName))
            sale.CashierName = _user.UserName;

        db.Sales.Add(sale);

        foreach (var item in sale.Items)
        {
            var inv = await db.InventoryItems
                .FirstOrDefaultAsync(i => i.StoreId == sale.StoreId && i.ProductId == item.ProductId);
            if (inv != null)
            {
                inv.QuantityOnHand = Math.Max(0, inv.QuantityOnHand - item.Quantity);
                inv.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Loyalty: 1 point per Spend-per-Point setting (NLe sales only)
        if (sale.CustomerId.HasValue && sale.Currency == "NLe" && loyaltyPer > 0)
        {
            var c = await db.Customers.FindAsync(sale.CustomerId.Value);
            if (c != null) c.LoyaltyPoints += (int)(sale.Total / loyaltyPer);
        }

        await db.SaveChangesAsync();
        await _audit.LogAsync("Created", "Sale", sale.Id, $"{sale.InvoiceNumber} total={sale.Total:N2}");
        return sale.Id;
    }

    public async Task CancelAsync(int id, string? reason = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var sale = await db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return;
        sale.Status = SaleStatus.Cancelled;
        foreach (var item in sale.Items)
        {
            var inv = await db.InventoryItems
                .FirstOrDefaultAsync(i => i.StoreId == sale.StoreId && i.ProductId == item.ProductId);
            if (inv != null) inv.QuantityOnHand += item.Quantity;
        }
        await db.SaveChangesAsync();
        await _audit.LogAsync("Cancelled", "Sale", sale.Id, reason);
    }

    public async Task RefundAsync(int id, string reason)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var loyaltyPer = await _settings.GetAsync(SettingKeys.LoyaltyPerCurrency, 100m);
        var sale = await db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return;
        sale.Status = SaleStatus.Refunded;
        foreach (var item in sale.Items)
        {
            var inv = await db.InventoryItems
                .FirstOrDefaultAsync(i => i.StoreId == sale.StoreId && i.ProductId == item.ProductId);
            if (inv != null) inv.QuantityOnHand += item.Quantity;
        }
        if (sale.CustomerId.HasValue && loyaltyPer > 0)
        {
            var c = await db.Customers.FindAsync(sale.CustomerId.Value);
            if (c != null) c.LoyaltyPoints = Math.Max(0, c.LoyaltyPoints - (int)(sale.Total / loyaltyPer));
        }
        await db.SaveChangesAsync();
        await _audit.LogAsync("Refunded", "Sale", sale.Id, reason);
    }
}
