using Microsoft.AspNetCore.Identity;

namespace SalesApp.Data.Identity;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public int? StoreId { get; set; }
    public int TenantId { get; set; } = 1;
}

public class IdentityAppDbContext : Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<ApplicationUser>
{
    public IdentityAppDbContext(Microsoft.EntityFrameworkCore.DbContextOptions<IdentityAppDbContext> options) : base(options) { }
}
