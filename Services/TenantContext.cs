namespace SalesApp.Services;

/// <summary>
/// Per-request tenant identity. The demo runs single-tenant (Id=1, "Default").
/// In production, resolve via subdomain / claim / header in middleware and inject.
/// </summary>
public class TenantContext
{
    public int TenantId { get; set; } = 1;
    public string TenantName { get; set; } = "Default";
}
