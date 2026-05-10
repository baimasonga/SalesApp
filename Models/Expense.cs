using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

public class Expense
{
    public int Id { get; set; }

    public int? StoreId { get; set; }
    public Store? Store { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    [Required, StringLength(160)]
    public string Description { get; set; } = string.Empty;

    [StringLength(60)]
    public string Category { get; set; } = "General";  // Rent, Utilities, Wages, Transport, Stock, Other

    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    [StringLength(60)]
    public string? PaidTo { get; set; }

    [StringLength(60)]
    public string? Reference { get; set; }

    [StringLength(80)]
    public string? RecordedBy { get; set; }
}
