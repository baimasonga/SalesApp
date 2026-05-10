using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;

namespace SalesApp.Services;

public record CommissionRow(int EmployeeId, string Name, decimal Sales, decimal Rate, decimal Commission);

public class EmployeeService
{
    private readonly IDbContextFactory<SalesDbContext> _factory;
    private readonly AuditService _audit;

    public EmployeeService(IDbContextFactory<SalesDbContext> factory, AuditService audit)
    { _factory = factory; _audit = audit; }

    public async Task<List<Employee>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Employees.Include(e => e.Store).OrderBy(e => e.FullName).ToListAsync();
    }

    public async Task SaveAsync(Employee e)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (e.Id == 0) db.Employees.Add(e); else db.Employees.Update(e);
        await db.SaveChangesAsync();
        await _audit.LogAsync(e.Id == 0 ? "Created" : "Updated", "Employee", e.Id, e.FullName);
    }

    public async Task<List<Shift>> GetShiftsAsync(int? employeeId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.Shifts.Include(s => s.Employee).Include(s => s.Store).AsQueryable();
        if (employeeId.HasValue) q = q.Where(s => s.EmployeeId == employeeId.Value);
        return await q.OrderByDescending(s => s.ClockIn).Take(200).ToListAsync();
    }

    public async Task ClockInAsync(int employeeId, int storeId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        // Auto-close any open shift first
        var open = await db.Shifts.FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.ClockOut == null);
        if (open != null) { open.ClockOut = DateTime.UtcNow; }
        db.Shifts.Add(new Shift { EmployeeId = employeeId, StoreId = storeId, ClockIn = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await _audit.LogAsync("ClockIn", "Shift", employeeId);
    }

    public async Task ClockOutAsync(int shiftId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var s = await db.Shifts.FindAsync(shiftId);
        if (s == null || s.ClockOut.HasValue) return;
        s.ClockOut = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await _audit.LogAsync("ClockOut", "Shift", shiftId);
    }

    /// <summary>
    /// Compute commissions for a date range. Sales are attributed to the cashier whose
    /// CashierName matches the employee's FullName (case-insensitive prefix match).
    /// </summary>
    public async Task<List<CommissionRow>> GetCommissionsAsync(DateTime from, DateTime to)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var employees = await db.Employees.Where(e => e.IsActive).ToListAsync();
        var sales = await db.Sales
            .Where(s => s.Status == SaleStatus.Completed && s.SaleDate >= from && s.SaleDate < to.AddDays(1))
            .Select(s => new { s.CashierName, s.Total })
            .ToListAsync();

        var rows = new List<CommissionRow>();
        foreach (var e in employees)
        {
            var first = e.FullName.Split(' ').FirstOrDefault() ?? e.FullName;
            var attributed = sales.Where(s =>
                !string.IsNullOrEmpty(s.CashierName) &&
                (s.CashierName.Equals(e.FullName, StringComparison.OrdinalIgnoreCase) ||
                 s.CashierName.StartsWith(first, StringComparison.OrdinalIgnoreCase)))
                .Sum(x => x.Total);
            var commission = Math.Round(attributed * e.CommissionRate, 2);
            rows.Add(new CommissionRow(e.Id, e.FullName, attributed, e.CommissionRate, commission));
        }
        return rows.OrderByDescending(r => r.Commission).ToList();
    }
}
