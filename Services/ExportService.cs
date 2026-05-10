using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

public class ExportService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    public ExportService(IDbContextFactory<SalesDbContext> factory) => _factory = factory;

    private static string Csv(string? s)
    {
        s ??= "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    public async Task<byte[]> SalesCsvAsync(DateTime? from, DateTime? to, int? storeId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.Sales.Include(s => s.Customer).Include(s => s.Store).AsQueryable();
        if (from.HasValue) q = q.Where(s => s.SaleDate >= from.Value);
        if (to.HasValue) q = q.Where(s => s.SaleDate < to.Value.AddDays(1));
        if (storeId.HasValue) q = q.Where(s => s.StoreId == storeId.Value);
        var rows = await q.OrderBy(s => s.SaleDate).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Invoice,Date,Store,Customer,Cashier,Payment,TxnRef,Status,Currency,Subtotal,Tax,Discount,Total");
        foreach (var s in rows)
        {
            sb.Append(Csv(s.InvoiceNumber)).Append(',');
            sb.Append(s.SaleDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Csv(s.Store?.Name)).Append(',');
            sb.Append(Csv(s.Customer?.FullName ?? "Walk-in")).Append(',');
            sb.Append(Csv(s.CashierName)).Append(',');
            sb.Append(s.PaymentMethod).Append(',');
            sb.Append(Csv(s.TransactionReference)).Append(',');
            sb.Append(s.Status).Append(',');
            sb.Append(s.Currency).Append(',');
            sb.Append(s.Subtotal.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(s.Tax.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(s.Discount.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(s.Total.ToString("F2", CultureInfo.InvariantCulture)).AppendLine();
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>NRA-friendly tax export: one row per sale with tax and customer info.</summary>
    public async Task<byte[]> NraTaxCsvAsync(DateTime from, DateTime to)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Sales
            .Include(s => s.Customer).Include(s => s.Store)
            .Where(s => s.Status == SaleStatus.Completed && s.SaleDate >= from && s.SaleDate < to.AddDays(1))
            .OrderBy(s => s.SaleDate)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("InvoiceNo,Date,Store,CustomerName,CustomerType,GrossAmount,TaxableAmount,GST_15pct,TotalIncTax,Currency");
        foreach (var s in rows)
        {
            sb.Append(Csv(s.InvoiceNumber)).Append(',');
            sb.Append(s.SaleDate.ToString("yyyy-MM-dd")).Append(',');
            sb.Append(Csv(s.Store?.Name)).Append(',');
            sb.Append(Csv(s.Customer?.FullName ?? "Walk-in")).Append(',');
            sb.Append(s.Customer?.Segment.ToString() ?? "Retail").Append(',');
            sb.Append(s.Subtotal.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(s.Subtotal.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(s.Tax.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(s.Total.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(s.Currency).AppendLine();
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> InventoryCsvAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.InventoryItems.Include(i => i.Store).Include(i => i.Product)
            .OrderBy(i => i.Store!.Name).ThenBy(i => i.Product!.Name).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Store,SKU,Product,Category,QuantityOnHand,ReorderLevel,LowStock,UnitCost,UnitPrice,InventoryValue");
        foreach (var i in rows)
        {
            var lowFlag = i.QuantityOnHand <= i.ReorderLevel ? "YES" : "NO";
            var value = i.QuantityOnHand * (i.Product?.CostPrice ?? 0);
            sb.Append(Csv(i.Store?.Name)).Append(',');
            sb.Append(Csv(i.Product?.Sku)).Append(',');
            sb.Append(Csv(i.Product?.Name)).Append(',');
            sb.Append(Csv(i.Product?.Category)).Append(',');
            sb.Append(i.QuantityOnHand).Append(',');
            sb.Append(i.ReorderLevel).Append(',');
            sb.Append(lowFlag).Append(',');
            sb.Append((i.Product?.CostPrice ?? 0).ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append((i.Product?.UnitPrice ?? 0).ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(value.ToString("F2", CultureInfo.InvariantCulture)).AppendLine();
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
