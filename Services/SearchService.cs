using Microsoft.EntityFrameworkCore;
using SalesApp.Data;

namespace SalesApp.Services;

public record SearchHit(string Type, string Title, string? Subtitle, string Href, string Icon);

public class SearchService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    public SearchService(IDbContextFactory<SalesDbContext> factory) { _factory = factory; }

    /// <summary>
    /// Mixed-type content search across customers, products, sales, employees.
    /// Returns at most <paramref name="perTypeLimit"/> hits per type.
    /// Designed to be cheap (LIKE-based; switch to FTS or a search index for scale).
    /// </summary>
    public async Task<List<SearchHit>> SearchAsync(string query, int perTypeLimit = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2) return new List<SearchHit>();
        var q = query.Trim();
        var qLower = q.ToLowerInvariant();
        await using var db = await _factory.CreateDbContextAsync(ct);

        var hits = new List<SearchHit>();

        // Customers — name / business / phone / email
        var cust = await db.Customers
            .Where(c => c.FullName.ToLower().Contains(qLower)
                     || (c.BusinessName != null && c.BusinessName.ToLower().Contains(qLower))
                     || (c.Phone != null && c.Phone.Contains(q))
                     || (c.Email != null && c.Email.ToLower().Contains(qLower)))
            .OrderBy(c => c.FullName).Take(perTypeLimit)
            .Select(c => new { c.Id, c.FullName, c.BusinessName })
            .ToListAsync(ct);
        foreach (var c in cust)
            hits.Add(new SearchHit("Customer", c.FullName, c.BusinessName, $"/customers/{c.Id}", "bi-person"));

        // Products — sku / name / barcode
        var prod = await db.Products
            .Where(p => p.IsActive && (
                p.Name.ToLower().Contains(qLower)
             || p.Sku.ToLower().Contains(qLower)
             || (p.Barcode != null && p.Barcode.Contains(q))))
            .OrderBy(p => p.Name).Take(perTypeLimit)
            .Select(p => new { p.Id, p.Name, p.Sku, p.Category, p.UnitPrice })
            .ToListAsync(ct);
        foreach (var p in prod)
            hits.Add(new SearchHit("Product", p.Name, $"{p.Sku} · {p.Category} · NLe {p.UnitPrice:N2}", "/products", "bi-tag"));

        // Sales — invoice number
        var sales = await db.Sales
            .Where(s => s.InvoiceNumber.Contains(q))
            .OrderByDescending(s => s.SaleDate).Take(perTypeLimit)
            .Select(s => new { s.Id, s.InvoiceNumber, s.SaleDate, s.Total, s.Currency, s.Status })
            .ToListAsync(ct);
        foreach (var s in sales)
            hits.Add(new SearchHit("Sale", s.InvoiceNumber,
                $"{s.SaleDate:dd MMM yyyy} · {s.Currency} {s.Total:N2} · {s.Status}",
                $"/sales/{s.Id}/print", "bi-receipt"));

        // Employees
        var emps = await db.Employees
            .Where(e => e.IsActive && (e.FullName.ToLower().Contains(qLower)
                     || (e.Phone != null && e.Phone.Contains(q))))
            .OrderBy(e => e.FullName).Take(perTypeLimit)
            .Select(e => new { e.Id, e.FullName, e.Role, e.Phone })
            .ToListAsync(ct);
        foreach (var e in emps)
            hits.Add(new SearchHit("Employee", e.FullName, $"{e.Role}{(string.IsNullOrEmpty(e.Phone) ? "" : " · " + e.Phone)}", "/employees", "bi-person-badge"));

        return hits;
    }
}
