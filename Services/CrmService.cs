using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

public class CrmService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    public CrmService(IDbContextFactory<SalesDbContext> factory) => _factory = factory;

    public async Task<PagedResult<Customer>> GetCustomersPagedAsync(int page, int pageSize, string? search = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(c =>
                c.FullName.ToLower().Contains(s) ||
                (c.BusinessName != null && c.BusinessName.ToLower().Contains(s)) ||
                (c.Phone != null && c.Phone.Contains(s)) ||
                (c.Email != null && c.Email.ToLower().Contains(s)));
        }
        return await q.OrderBy(c => c.FullName).ToPagedAsync(page, pageSize);
    }

    public async Task<List<Customer>> GetCustomersAsync(string? search = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(c =>
                c.FullName.ToLower().Contains(s) ||
                (c.BusinessName != null && c.BusinessName.ToLower().Contains(s)) ||
                (c.Phone != null && c.Phone.Contains(s)) ||
                (c.Email != null && c.Email.ToLower().Contains(s)));
        }
        return await q.OrderBy(c => c.FullName).ToListAsync();
    }

    public async Task<Customer?> GetAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Customers
            .Include(c => c.Interactions.OrderByDescending(i => i.When))
            .Include(c => c.Sales.OrderByDescending(s => s.SaleDate))
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task SaveAsync(Customer customer)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (customer.Id == 0) db.Customers.Add(customer); else db.Customers.Update(customer);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var c = await db.Customers.Include(x => x.Interactions).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return;
        // Detach customer from sales (becomes walk-in)
        var sales = db.Sales.Where(s => s.CustomerId == id);
        foreach (var s in sales) s.CustomerId = null;
        // Cascade interactions
        db.Interactions.RemoveRange(c.Interactions);
        db.Customers.Remove(c);
        await db.SaveChangesAsync();
    }

    public async Task LogInteractionAsync(Interaction interaction)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Interactions.Add(interaction);
        await db.SaveChangesAsync();
    }

    public async Task UpdateInteractionAsync(Interaction interaction)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Interactions.Update(interaction);
        await db.SaveChangesAsync();
    }

    public async Task DeleteInteractionAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var i = await db.Interactions.FindAsync(id);
        if (i != null) { db.Interactions.Remove(i); await db.SaveChangesAsync(); }
    }

    public async Task<List<Interaction>> GetUpcomingFollowUpsAsync(int days = 14)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var cutoff = DateTime.UtcNow.AddDays(days);
        return await db.Interactions
            .Include(i => i.Customer)
            .Where(i => i.FollowUpDate != null && i.FollowUpDate <= cutoff)
            .OrderBy(i => i.FollowUpDate)
            .ToListAsync();
    }
}
