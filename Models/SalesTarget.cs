using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

public class SalesTarget
{
    public int Id { get; set; }

    public int? StoreId { get; set; } // null = company-wide
    public Store? Store { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

    public decimal TargetAmount { get; set; }

    [StringLength(200)]
    public string? Notes { get; set; }
}
