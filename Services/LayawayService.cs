using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

public class LayawayService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    private readonly AuditService _audit;

    public LayawayService(IDbContextFactory<SalesDbContext> factory, AuditService audit)
    { _factory = factory; _audit = audit; }

    public async Task<List<Layaway>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Layaways
            .Include(l => l.Customer).Include(l => l.Store).Include(l => l.Payments)
            .OrderByDescending(l => l.StartDate).ToListAsync();
    }

    public async Task<Layaway?> GetAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Layaways
            .Include(l => l.Customer).Include(l => l.Store).Include(l => l.Payments)
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<int> CreateAsync(Layaway l)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (string.IsNullOrWhiteSpace(l.LayawayNumber))
            l.LayawayNumber = $"LAY-{DateTime.UtcNow:yyyyMMddHHmmss}";
        db.Layaways.Add(l);
        await db.SaveChangesAsync();
        await _audit.LogAsync("Created", "Layaway", l.Id, l.LayawayNumber);
        return l.Id;
    }

    public async Task AddPaymentAsync(int layawayId, LayawayPayment payment)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var l = await db.Layaways.Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == layawayId);
        if (l == null) return;
        payment.LayawayId = layawayId;
        l.Payments.Add(payment);
        l.AmountPaid += payment.Amount;
        if (l.AmountPaid >= l.TotalAmount)
        {
            l.Status = LayawayStatus.Completed;
            l.CompletedDate = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        await _audit.LogAsync("Payment", "Layaway", layawayId, $"+{payment.Amount:N2}");
    }

    public async Task CancelAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var l = await db.Layaways.FindAsync(id);
        if (l == null) return;
        l.Status = LayawayStatus.Cancelled;
        await db.SaveChangesAsync();
        await _audit.LogAsync("Cancelled", "Layaway", id);
    }
}
