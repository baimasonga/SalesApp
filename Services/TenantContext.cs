namespace SalesApp.Services;

/// <summary>
/// Per-circuit tenant identity. When <see cref="Enabled"/> is true the EF
/// global query filter constrains every ITenantScoped entity to <see cref="TenantId"/>,
/// and SaveChanges auto-fills TenantId on insert.
///
/// When Enabled is false (default for the single-tenant demo) the system
/// runs without isolation — all rows visible.
/// </summary>
public class TenantContext
{
    public int TenantId { get; set; } = 1;
    public string TenantName { get; set; } = "Default";
    public bool Enabled { get; set; } = false; // Flip to true once tenants are configured

    public event Action? Changed;

    public void Switch(int id, string name)
    {
        if (TenantId == id && TenantName == name) return;
        TenantId = id;
        TenantName = name;
        Changed?.Invoke();
    }
}
