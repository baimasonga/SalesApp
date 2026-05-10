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

    /// <summary>
    /// NRA Monthly GST Return — formatted to mirror the standard Sierra Leone
    /// National Revenue Authority Goods & Services Tax filing template.
    ///
    /// Structure:
    ///   - Header block: taxpayer identity (TIN, business name, address), period
    ///   - Summary block: totals (taxable sales, GST collected, exempt, credits, net payable)
    ///   - Detail block: one row per completed invoice in the period
    ///
    /// Adjust column ordering / labels per your latest NRA Form 003 instruction.
    /// </summary>
    public async Task<byte[]> NraTaxCsvAsync(DateTime from, DateTime to)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Sales
            .Include(s => s.Customer).Include(s => s.Store)
            .Where(s => s.Status == SaleStatus.Completed && s.SaleDate >= from && s.SaleDate < to.AddDays(1))
            .OrderBy(s => s.SaleDate)
            .ToListAsync();

        // Read taxpayer info from settings
        async Task<string> S(string key, string fallback)
        {
            var s = await db.Settings.FindAsync(key);
            return s?.Value ?? fallback;
        }
        var businessName = await S("Business.Name", "Demo Business SL");
        var tin          = await S("Business.NraTin", "");
        var address      = await S("Business.Address", "");
        var phone        = await S("Business.Phone", "");
        var email        = await S("Business.Email", "");

        var taxableSales = rows.Sum(s => s.Subtotal);
        var gstCollected = rows.Sum(s => s.Tax);
        var totalSales   = rows.Sum(s => s.Total);

        var sb = new StringBuilder();

        // --- Header block ---
        sb.AppendLine("# NRA GOODS & SERVICES TAX MONTHLY RETURN");
        sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();
        sb.AppendLine("Taxpayer Information");
        sb.AppendLine($"Business Name,{Csv(businessName)}");
        sb.AppendLine($"NRA TIN,{Csv(tin)}");
        sb.AppendLine($"Address,{Csv(address)}");
        sb.AppendLine($"Phone,{Csv(phone)}");
        sb.AppendLine($"Email,{Csv(email)}");
        sb.AppendLine($"Period From,{from:yyyy-MM-dd}");
        sb.AppendLine($"Period To,{to:yyyy-MM-dd}");
        sb.AppendLine();

        // --- Summary block ---
        sb.AppendLine("Return Summary,Amount (NLe)");
        sb.AppendLine($"Total taxable sales,{taxableSales.ToString("F2", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Total GST output tax (15%),{gstCollected.ToString("F2", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Total exempt / zero-rated sales,0.00");
        sb.AppendLine($"Input tax credits,0.00");
        sb.AppendLine($"Net GST payable,{gstCollected.ToString("F2", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Invoice count,{rows.Count}");
        sb.AppendLine();

        // --- Detail block ---
        sb.AppendLine("Invoice Detail");
        sb.AppendLine("InvoiceNo,Date,Store,CustomerName,CustomerTIN,CustomerType,Currency,TaxableAmount,GST_15pct,TotalIncTax,PaymentMethod,TxnRef");
        foreach (var s in rows)
        {
            sb.Append(Csv(s.InvoiceNumber)).Append(',');
            sb.Append(s.SaleDate.ToString("yyyy-MM-dd")).Append(',');
            sb.Append(Csv(s.Store?.Name)).Append(',');
            sb.Append(Csv(s.Customer?.FullName ?? "Walk-in")).Append(',');
            sb.Append(Csv("")).Append(','); // Customer TIN (not captured per-customer yet)
            sb.Append(s.Customer?.Segment.ToString() ?? "Retail").Append(',');
            sb.Append(s.Currency).Append(',');
            sb.Append(s.Subtotal.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(s.Tax.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(s.Total.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(s.PaymentMethod).Append(',');
            sb.Append(Csv(s.TransactionReference)).AppendLine();
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> AuditCsvAsync(string? action, string? entityType, string? user, DateTime? from, DateTime? to)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.AuditLogs.AsQueryable();
        if (!string.IsNullOrEmpty(action))     q = q.Where(a => a.Action == action);
        if (!string.IsNullOrEmpty(entityType)) q = q.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrEmpty(user))       q = q.Where(a => a.UserName == user);
        if (from.HasValue)                     q = q.Where(a => a.Timestamp >= from.Value);
        if (to.HasValue)                       q = q.Where(a => a.Timestamp < to.Value.AddDays(1));
        var rows = await q.OrderByDescending(a => a.Timestamp).Take(5000).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,User,Action,EntityType,EntityId,Details");
        foreach (var r in rows)
        {
            sb.Append(r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Csv(r.UserName)).Append(',');
            sb.Append(Csv(r.Action)).Append(',');
            sb.Append(Csv(r.EntityType)).Append(',');
            sb.Append(r.EntityId).Append(',');
            sb.Append(Csv(r.Details)).AppendLine();
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
