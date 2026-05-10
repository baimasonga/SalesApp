using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

public class Employee
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(40)]
    public string Role { get; set; } = "Cashier";

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(120)]
    public string? Email { get; set; }

    public int? StoreId { get; set; }
    public Store? Store { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CommissionRate { get; set; } = 0.02m; // 2%

    [Range(0, double.MaxValue)]
    public decimal MonthlySalary { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Shift> Shifts { get; set; } = new();
}

public class Shift
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int StoreId { get; set; }
    public Store? Store { get; set; }

    public DateTime ClockIn { get; set; } = DateTime.UtcNow;
    public DateTime? ClockOut { get; set; }

    [StringLength(200)]
    public string? Notes { get; set; }

    public TimeSpan Duration => (ClockOut ?? DateTime.UtcNow) - ClockIn;
}
