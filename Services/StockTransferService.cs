using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

public class StockTransferService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    private readonly AuditService _audit;

    public StockTransferService(IDbContextFactory<SalesDbContext> factory, AuditService audit)
    { _factory = factory; _audit = audit; }

    public async Task<List<StockTransfer>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.StockTransfers
            .Include(t => t.FromStore).Include(t => t.ToStore).Include(t => t.Items)
            .OrderByDescending(t => t.TransferDate).ToListAsync();
    }

    public async Task<StockTransfer?> GetAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.StockTransfers
            .Include(t => t.FromStore).Include(t => t.ToStore).Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<int> CreateAsync(StockTransfer t)
    {
        if (t.FromStoreId == t.ToStoreId)
            throw new InvalidOperationException("From and To stores must differ.");
        await using var db = await _factory.CreateDbContextAsync();
        if (string.IsNullOrWhiteSpace(t.TransferNumber))
            t.TransferNumber = $"TRF-{DateTime.UtcNow:yyyyMMddHHmmss}";
        db.StockTransfers.Add(t);
        await db.SaveChangesAsync();
        await _audit.LogAsync("Created", "StockTransfer", t.Id, t.TransferNumber);
        return t.Id;
    }

    public async Task CompleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var t = await db.StockTransfers.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (t == null || t.Status != StockTransferStatus.Pending) return;

        foreach (var line in t.Items)
        {
            var fromInv = await db.InventoryItems.FirstOrDefaultAsync(i => i.StoreId == t.FromStoreId && i.ProductId == line.ProductId);
            var toInv = await db.InventoryItems.FirstOrDefaultAsync(i => i.StoreId == t.ToStoreId && i.ProductId == line.ProductId);
            if (fromInv == null || fromInv.QuantityOnHand < line.Quantity)
                throw new InvalidOperationException($"Insufficient stock at source for {line.ProductName}.");

            fromInv.QuantityOnHand -= line.Quantity;
            fromInv.UpdatedAt = DateTime.UtcNow;
            if (toInv == null)
                db.InventoryItems.Add(new InventoryItem { StoreId = t.ToStoreId, ProductId = line.ProductId, QuantityOnHand = line.Quantity });
            else
            {
                toInv.QuantityOnHand += line.Quantity;
                toInv.UpdatedAt = DateTime.UtcNow;
            }
        }
        t.Status = StockTransferStatus.Completed;
        await db.SaveChangesAsync();
        await _audit.LogAsync("Completed", "StockTransfer", t.Id, t.TransferNumber);
    }

    public async Task CancelAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var t = await db.StockTransfers.FindAsync(id);
        if (t == null) return;
        t.Status = StockTransferStatus.Cancelled;
        await db.SaveChangesAsync();
        await _audit.LogAsync("Cancelled", "StockTransfer", t.Id, t.TransferNumber);
    }
}
