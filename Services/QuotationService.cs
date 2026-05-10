using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

public class QuotationService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    private readonly AuditService _audit;
    private readonly SalesService _sales;
    private readonly SettingsService _settings;

    public QuotationService(IDbContextFactory<SalesDbContext> factory, AuditService audit, SalesService sales, SettingsService settings)
    { _factory = factory; _audit = audit; _sales = sales; _settings = settings; }

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
        var gstRate = await _settings.GetAsync(SettingKeys.GstRate, 0.15m);
        foreach (var i in q.Items) i.LineTotal = i.Quantity * i.UnitPrice;
        q.Subtotal = q.Items.Sum(i => i.LineTotal);
        q.Tax = Math.Round(q.Subtotal * gstRate, 2);
        q.Total = q.Subtotal + q.Tax - q.Discount;
        db.Quotations.Add(q);
        await db.SaveChangesAsync();
        await _audit.LogAsync("Created", "Quotation", q.Id, q.QuoteNumber);
        return q.Id;
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = await db.Quotations.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (q == null) return;
        if (q.Status == QuotationStatus.Converted)
            throw new InvalidOperationException("Converted quotations cannot be deleted; the linked sale exists.");
        db.QuotationItems.RemoveRange(q.Items);
        db.Quotations.Remove(q);
        await db.SaveChangesAsync();
        await _audit.LogAsync("Deleted", "Quotation", id, q.QuoteNumber);
    }

    public async Task UpdateAsync(Quotation q)
    {
        if (q.Id == 0) throw new InvalidOperationException("Use CreateAsync for new quotations.");
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.Quotations.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == q.Id);
        if (existing == null) throw new InvalidOperationException("Quotation not found.");
        if (existing.Status != QuotationStatus.Draft && existing.Status != QuotationStatus.Sent)
            throw new InvalidOperationException("Only Draft/Sent quotations can be edited.");
        existing.CustomerId = q.CustomerId;
        existing.StoreId = q.StoreId;
        existing.ValidUntil = q.ValidUntil;
        existing.Notes = q.Notes;
        existing.Discount = q.Discount;
        db.QuotationItems.RemoveRange(existing.Items);
        existing.Items = q.Items.Select(i => new QuotationItem
        {
            ProductId = i.ProductId, ProductName = i.ProductName,
            Quantity = i.Quantity, UnitPrice = i.UnitPrice,
            LineTotal = i.Quantity * i.UnitPrice
        }).ToList();
        var gstRate = await _settings.GetAsync(SettingKeys.GstRate, 0.15m);
        existing.Subtotal = existing.Items.Sum(i => i.LineTotal);
        existing.Tax = Math.Round(existing.Subtotal * gstRate, 2);
        existing.Total = existing.Subtotal + existing.Tax - existing.Discount;
        await db.SaveChangesAsync();
        await _audit.LogAsync("Updated", "Quotation", q.Id, existing.QuoteNumber);
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
