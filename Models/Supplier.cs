using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

public class Supplier : ITenantScoped
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required, StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(120)]
    public string? Email { get; set; }

    [StringLength(120)]
    public string? Country { get; set; } = "Sierra Leone";

    [StringLength(250)]
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<PurchaseOrder> PurchaseOrders { get; set; } = new();
}
