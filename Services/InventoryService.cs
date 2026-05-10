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

    public async Task<PagedResult<Product>> GetProductsPagedAsync(int page, int pageSize, string? search = null, string? category = null, bool includeInactive = false)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.Products.AsQueryable();
        if (!includeInactive) q = q.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(p => p.Category == category);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(p => p.Name.ToLower().Contains(s) || p.Sku.ToLower().Contains(s) ||
                             (p.Barcode != null && p.Barcode.Contains(search)));
        }
        return await q.OrderBy(p => p.Name).ToPagedAsync(page, pageSize);
    }

    public async Task<PagedResult<InventoryItem>> GetInventoryPagedAsync(int page, int pageSize, int? storeId = null, bool lowOnly = false)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.InventoryItems.Include(i => i.Product).Include(i => i.Store).AsQueryable();
        if (storeId.HasValue) q = q.Where(i => i.StoreId == storeId.Value);
        if (lowOnly) q = q.Where(i => i.QuantityOnHand <= i.ReorderLevel);
        return await q.OrderBy(i => i.Store!.Name).ThenBy(i => i.Product!.Name).ToPagedAsync(page, pageSize);
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

    public async Task DeleteStoreAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var s = await db.Stores.FindAsync(id);
        if (s == null) return;
        var hasSales = await db.Sales.AnyAsync(x => x.StoreId == id);
        var hasInv   = await db.InventoryItems.AnyAsync(x => x.StoreId == id && x.QuantityOnHand > 0);
        if (hasSales || hasInv)
        {
            // Soft-delete: deactivate
            s.IsActive = false;
            await db.SaveChangesAsync();
            throw new InvalidOperationException("Store has sales or stock; deactivated instead of deleting.");
        }
        // Remove zero-stock inventory rows for this store
        var emptyInv = db.InventoryItems.Where(x => x.StoreId == id);
        db.InventoryItems.RemoveRange(emptyInv);
        db.Stores.Remove(s);
        await db.SaveChangesAsync();
    }

    public async Task DeleteProductAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var p = await db.Products.FindAsync(id);
        if (p == null) return;
        var hasSales = await db.SaleItems.AnyAsync(x => x.ProductId == id);
        if (hasSales)
        {
            p.IsActive = false;
            await db.SaveChangesAsync();
            throw new InvalidOperationException("Product has sales history; deactivated (soft-delete) instead.");
        }
        var inv = db.InventoryItems.Where(x => x.ProductId == id);
        db.InventoryItems.RemoveRange(inv);
        db.Products.Remove(p);
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
