using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

public class SalesService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    public SalesService(IDbContextFactory<SalesDbContext> factory) => _factory = factory;

    public async Task<List<Sale>> GetRecentAsync(int take = 50)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Sales
            .Include(s => s.Customer)
            .Include(s => s.Store)
            .Include(s => s.Items)
            .OrderByDescending(s => s.SaleDate)
            .Take(take)
            .ToListAsync();
    }

    public async Task<Sale?> GetAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Sales
            .Include(s => s.Customer)
            .Include(s => s.Store)
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<int> CreateAsync(Sale sale)
    {
        await using var db = await _factory.CreateDbContextAsync();

        sale.Subtotal = sale.Items.Sum(i => i.LineTotal);
        sale.Tax = Math.Round(sale.Subtotal * 0.15m, 2);
        sale.Total = sale.Subtotal + sale.Tax - sale.Discount;
        if (string.IsNullOrWhiteSpace(sale.InvoiceNumber))
            sale.InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}";

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

        await db.SaveChangesAsync();
        return sale.Id;
    }

    public async Task CancelAsync(int id)
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
    }
}
