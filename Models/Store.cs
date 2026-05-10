using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

public class Store : ITenantScoped
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Location { get; set; } = "Freetown";

    [StringLength(120)]
    public string? District { get; set; } = "Western Area Urban";

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(120)]
    public string? ManagerName { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<InventoryItem> InventoryItems { get; set; } = new();
    public List<Sale> Sales { get; set; } = new();
}
