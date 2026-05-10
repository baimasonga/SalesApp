using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

public class Customer
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(120)]
    public string? BusinessName { get; set; }

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(120)]
    public string? Email { get; set; }

    [StringLength(120)]
    public string? District { get; set; } = "Western Area Urban";

    [StringLength(250)]
    public string? Address { get; set; }

    public CustomerSegment Segment { get; set; } = CustomerSegment.Retail;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(500)]
    public string? Notes { get; set; }

    public int LoyaltyPoints { get; set; } = 0;

    public List<Sale> Sales { get; set; } = new();
    public List<Interaction> Interactions { get; set; } = new();
}

public enum CustomerSegment
{
    Retail,
    Wholesale,
    Corporate,
    Government,
    NGO
}
