using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

public class PurchaseOrderService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    private readonly AuditService _audit;

    public PurchaseOrderService(IDbContextFactory<SalesDbContext> factory, AuditService audit)
    { _factory = factory; _audit = audit; }

    public async Task<List<Supplier>> GetSuppliersAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Suppliers.OrderBy(s => s.Name).ToListAsync();
    }

    public async Task SaveSupplierAsync(Supplier s)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (s.Id == 0) db.Suppliers.Add(s); else db.Suppliers.Update(s);
        await db.SaveChangesAsync();
    }

    public async Task DeleteSupplierAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var s = await db.Suppliers.FindAsync(id);
        if (s == null) return;
        var hasPos = await db.PurchaseOrders.AnyAsync(x => x.SupplierId == id);
        if (hasPos)
        {
            s.IsActive = false;
            await db.SaveChangesAsync();
            throw new InvalidOperationException("Supplier has purchase-order history; deactivated instead of deleting.");
        }
        db.Suppliers.Remove(s);
        await db.SaveChangesAsync();
    }

    public async Task<List<PurchaseOrder>> GetPosAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.PurchaseOrders
            .Include(p => p.Supplier).Include(p => p.Store).Include(p => p.Items)
            .OrderByDescending(p => p.OrderDate).ToListAsync();
    }

    public async Task<PurchaseOrder?> GetAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.PurchaseOrders
            .Include(p => p.Supplier).Include(p => p.Store).Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<int> CreateAsync(PurchaseOrder po)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (string.IsNullOrWhiteSpace(po.PoNumber))
            po.PoNumber = $"PO-{DateTime.UtcNow:yyyyMMddHHmmss}";
        foreach (var i in po.Items) i.LineTotal = i.Quantity * i.UnitCost;
        po.Total = po.Items.Sum(i => i.LineTotal);
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();
        await _audit.LogAsync("Created", "PurchaseOrder", po.Id, po.PoNumber);
        return po.Id;
    }

    public async Task ReceiveAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var po = await db.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id);
        if (po == null || po.Status == PurchaseOrderStatus.Received) return;

        foreach (var line in po.Items)
        {
            var inv = await db.InventoryItems.FirstOrDefaultAsync(i => i.StoreId == po.StoreId && i.ProductId == line.ProductId);
            if (inv == null)
            {
                db.InventoryItems.Add(new InventoryItem { StoreId = po.StoreId, ProductId = line.ProductId, QuantityOnHand = line.Quantity });
            }
            else
            {
                inv.QuantityOnHand += line.Quantity;
                inv.UpdatedAt = DateTime.UtcNow;
            }
        }
        po.Status = PurchaseOrderStatus.Received;
        po.ReceivedDate = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await _audit.LogAsync("Received", "PurchaseOrder", po.Id, po.PoNumber);
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var po = await db.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id);
        if (po == null) return;
        if (po.Status == PurchaseOrderStatus.Received)
            throw new InvalidOperationException("Received POs cannot be deleted (stock has already been credited).");
        db.PurchaseOrderItems.RemoveRange(po.Items);
        db.PurchaseOrders.Remove(po);
        await db.SaveChangesAsync();
        await _audit.LogAsync("Deleted", "PurchaseOrder", id, po.PoNumber);
    }

    public async Task CancelAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var po = await db.PurchaseOrders.FindAsync(id);
        if (po == null) return;
        po.Status = PurchaseOrderStatus.Cancelled;
        await db.SaveChangesAsync();
        await _audit.LogAsync("Cancelled", "PurchaseOrder", po.Id, po.PoNumber);
    }
}
