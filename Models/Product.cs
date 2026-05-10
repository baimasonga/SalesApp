using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

public class Product : ITenantScoped
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required, StringLength(40)]
    public string Sku { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(80)]
    public string? Category { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CostPrice { get; set; }

    [StringLength(20)]
    public string Unit { get; set; } = "pcs";

    [StringLength(50)]
    public string? Barcode { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<InventoryItem> InventoryItems { get; set; } = new();
}
