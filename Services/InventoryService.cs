using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

public class InventoryService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    public InventoryService(IDbContextFactory<SalesDbContext> factory) => _factory = factory;

    public async Task<List<Store>> GetStoresAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Stores.OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<List<Product>> GetProductsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
    }

    public async Task<List<InventoryItem>> GetInventoryAsync(int? storeId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.InventoryItems.Include(i => i.Product).Include(i => i.Store).AsQueryable();
        if (storeId.HasValue) q = q.Where(i => i.StoreId == storeId.Value);
        return await q.OrderBy(i => i.Store!.Name).ThenBy(i => i.Product!.Name).ToListAsync();
    }

    public async Task<List<InventoryItem>> GetLowStockAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.InventoryItems
            .Include(i => i.Product).Include(i => i.Store)
            .Where(i => i.QuantityOnHand <= i.ReorderLevel)
            .OrderBy(i => i.QuantityOnHand)
            .ToListAsync();
    }

    public async Task SaveStoreAsync(Store store)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (store.Id == 0) db.Stores.Add(store); else db.Stores.Update(store);
        await db.SaveChangesAsync();
    }

    public async Task SaveProductAsync(Product product)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (product.Id == 0) db.Products.Add(product); else db.Products.Update(product);
        await db.SaveChangesAsync();
    }

    public async Task AdjustStockAsync(int storeId, int productId, int delta)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var inv = await db.InventoryItems
            .FirstOrDefaultAsync(i => i.StoreId == storeId && i.ProductId == productId);
        if (inv == null)
        {
            inv = new InventoryItem { StoreId = storeId, ProductId = productId, QuantityOnHand = Math.Max(0, delta) };
            db.InventoryItems.Add(inv);
        }
        else
        {
            inv.QuantityOnHand = Math.Max(0, inv.QuantityOnHand + delta);
            inv.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }
}
