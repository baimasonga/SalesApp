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

    public async Task<PagedResult<Sale>> GetPagedAsync(int page, int pageSize,
        DateTime? from = null, DateTime? to = null, int? storeId = null, SaleStatus? status = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.Sales
            .Include(s => s.Customer)
            .Include(s => s.Store)
            .Include(s => s.Items)
            .AsQueryable();
        if (from.HasValue)     q = q.Where(s => s.SaleDate >= from.Value);
        if (to.HasValue)       q = q.Where(s => s.SaleDate < to.Value.AddDays(1));
        if (storeId.HasValue)  q = q.Where(s => s.StoreId == storeId.Value);
        if (status.HasValue)   q = q.Where(s => s.Status == status.Value);
        return await q.OrderByDescending(s => s.SaleDate).ToPagedAsync(page, pageSize);
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
        // Transaction guarantees: either the sale, ALL inventory decrements, and
        // the loyalty bump commit together, or nothing does. Prevents half-saved
        // sales where stock is decremented but the sale row was rolled back.
        var supportsTx = db.Database.IsRelational();
        var tx = supportsTx ? await db.Database.BeginTransactionAsync() : null;
        try
        {
        var gstRate = await _settings.GetAsync(SettingKeys.GstRate, 0.15m);
        var loyaltyPer = await _settings.GetAsync(SettingKeys.LoyaltyPerCurrency, 100m);
        var discountCap = await _settings.GetAsync(SettingKeys.DefaultDiscountCap, 0.20m);

        // Recompute totals server-side
        foreach (var i in sale.Items)
        {
            i.LineTotal = (i.Quantity * i.UnitPrice) - i.LineDiscount;
        }
        sale.Subtotal = sale.Items.Sum(i => i.LineTotal);
        sale.Tax = Math.Round(sale.Subtotal * gstRate, 2);
        sale.Total = sale.Subtotal + sale.Tax - sale.Discount;

        // Enforce discount cap from settings: total discount can't exceed cap × subtotal
        // (managers can lift the cap in /settings if they need to grant a larger discount)
        if (discountCap > 0 && sale.Subtotal > 0)
        {
            var totalDiscount = sale.Discount + sale.Items.Sum(i => i.LineDiscount);
            var maxAllowed = sale.Subtotal * discountCap;
            if (totalDiscount > maxAllowed)
                throw new InvalidOperationException(
                    $"Total discount of {totalDiscount:N2} exceeds the configured cap of " +
                    $"{discountCap:P0} (max {maxAllowed:N2}). Adjust the discount or raise the cap in Settings.");
        }
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
            if (tx != null) await tx.CommitAsync();
            await _audit.LogAsync("Created", "Sale", sale.Id, $"{sale.InvoiceNumber} total={sale.Total:N2}");
            return sale.Id;
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync();
            throw;
        }
        finally
        {
            if (tx != null) await tx.DisposeAsync();
        }
    }

    public async Task CancelAsync(int id, string? reason = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        try
        {
            var sale = await db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id);
            if (sale == null) { if (tx != null) await tx.RollbackAsync(); return; }
            sale.Status = SaleStatus.Cancelled;
            foreach (var item in sale.Items)
            {
                var inv = await db.InventoryItems
                    .FirstOrDefaultAsync(i => i.StoreId == sale.StoreId && i.ProductId == item.ProductId);
                if (inv != null) inv.QuantityOnHand += item.Quantity;
            }
            await db.SaveChangesAsync();
            if (tx != null) await tx.CommitAsync();
            await _audit.LogAsync("Cancelled", "Sale", sale.Id, reason);
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync();
            throw;
        }
        finally { if (tx != null) await tx.DisposeAsync(); }
    }

    public async Task RefundAsync(int id, string reason)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        try
        {
            var loyaltyPer = await _settings.GetAsync(SettingKeys.LoyaltyPerCurrency, 100m);
            var refundWindow = await _settings.GetAsync(SettingKeys.RefundWindowDays, 30);
            var sale = await db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id);
            if (sale == null) { if (tx != null) await tx.RollbackAsync(); return; }

            // Enforce refund window from settings
            var daysSince = (DateTime.UtcNow - sale.SaleDate).TotalDays;
            if (refundWindow > 0 && daysSince > refundWindow)
                throw new InvalidOperationException(
                    $"Sale was {Math.Floor(daysSince):F0} days ago. Refund window is {refundWindow} days. " +
                    $"Adjust the window in Settings if needed.");

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
            if (tx != null) await tx.CommitAsync();
            await _audit.LogAsync("Refunded", "Sale", sale.Id, reason);
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync();
            throw;
        }
        finally { if (tx != null) await tx.DisposeAsync(); }
    }
}
