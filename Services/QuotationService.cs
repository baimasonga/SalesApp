using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

public class QuotationService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    private readonly AuditService _audit;
    private readonly SalesService _sales;

    public QuotationService(IDbContextFactory<SalesDbContext> factory, AuditService audit, SalesService sales)
    { _factory = factory; _audit = audit; _sales = sales; }

    public async Task<List<Quotation>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Quotations
            .Include(q => q.Customer).Include(q => q.Store).Include(q => q.Items)
            .OrderByDescending(q => q.QuoteDate).ToListAsync();
    }

    public async Task<Quotation?> GetAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Quotations
            .Include(q => q.Customer).Include(q => q.Store).Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<int> CreateAsync(Quotation q)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (string.IsNullOrWhiteSpace(q.QuoteNumber))
            q.QuoteNumber = $"QT-{DateTime.UtcNow:yyyyMMddHHmmss}";
        foreach (var i in q.Items) i.LineTotal = i.Quantity * i.UnitPrice;
        q.Subtotal = q.Items.Sum(i => i.LineTotal);
        q.Tax = Math.Round(q.Subtotal * 0.15m, 2);
        q.Total = q.Subtotal + q.Tax - q.Discount;
        db.Quotations.Add(q);
        await db.SaveChangesAsync();
        await _audit.LogAsync("Created", "Quotation", q.Id, q.QuoteNumber);
        return q.Id;
    }

    public async Task SetStatusAsync(int id, QuotationStatus status)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = await db.Quotations.FindAsync(id);
        if (q == null) return;
        q.Status = status;
        await db.SaveChangesAsync();
        await _audit.LogAsync(status.ToString(), "Quotation", q.Id, q.QuoteNumber);
    }

    public async Task<int> ConvertToSaleAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = await db.Quotations.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (q == null) throw new InvalidOperationException("Quotation not found.");

        var sale = new Sale
        {
            CustomerId = q.CustomerId,
            StoreId = q.StoreId,
            SaleDate = DateTime.UtcNow,
            PaymentMethod = PaymentMethod.Cash,
            Status = SaleStatus.Completed,
            Discount = q.Discount,
            Notes = $"From quote {q.QuoteNumber}",
            Items = q.Items.Select(i => new SaleItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineTotal = i.LineTotal
            }).ToList()
        };
        var saleId = await _sales.CreateAsync(sale);

        // Re-load and mark converted
        await using var db2 = await _factory.CreateDbContextAsync();
        var qq = await db2.Quotations.FindAsync(id);
        if (qq != null)
        {
            qq.Status = QuotationStatus.Converted;
            qq.ConvertedSaleId = saleId;
            await db2.SaveChangesAsync();
        }
        await _audit.LogAsync("Converted", "Quotation", id, $"-> Sale {saleId}");
        return saleId;
    }
}
