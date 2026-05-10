using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models;

/// <summary>
/// Multi-tenant scaffolding — each row in any tenant-scoped entity carries a TenantId.
/// In the demo there is exactly one tenant ("Default") and the filter is permissive.
/// To enable: add ITenantScoped to entities and uncomment the global query filter
/// in SalesDbContext.OnModelCreating.
/// </summary>
public class Tenant
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Subdomain { get; set; }

    [StringLength(80)]
    public string? Slug { get; set; }

    [StringLength(80)]
    public string Country { get; set; } = "Sierra Leone";

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(120)]
    public string? Email { get; set; }

    [StringLength(8)]
    public string DefaultCurrency { get; set; } = "NLe";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public interface ITenantScoped
{
    int TenantId { get; set; }
}
