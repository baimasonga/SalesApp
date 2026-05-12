using Microsoft.EntityFrameworkCore;
using SalesApp.Models;
using SalesApp.Services;

namespace SalesApp.Data;

public class SalesDbContext : DbContext
{
    private readonly TenantContext? _tenant;

    // Single constructor — TenantContext is optional so the factory can still
    // create contexts during seeding before any circuit/tenant is established.
    public SalesDbContext(DbContextOptions<SalesDbContext> options, TenantContext? tenant = null) : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Interaction> Interactions => Set<Interaction>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferItem> StockTransferItems => Set<StockTransferItem>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
    public DbSet<SalesTarget> SalesTargets => Set<SalesTarget>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Layaway> Layaways => Set<Layaway>();
    public DbSet<LayawayPayment> LayawayPayments => Set<LayawayPayment>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<MoMoTransaction> MoMoTransactions => Set<MoMoTransaction>();
    public DbSet<SmsLog> SmsLogs => Set<SmsLog>();
    public DbSet<SavedView> SavedViews => Set<SavedView>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>().HasIndex(p => p.Sku).IsUnique();
        b.Entity<Sale>().HasIndex(s => s.InvoiceNumber).IsUnique();
        b.Entity<PurchaseOrder>().HasIndex(s => s.PoNumber).IsUnique();
        b.Entity<StockTransfer>().HasIndex(s => s.TransferNumber).IsUnique();
        b.Entity<Quotation>().HasIndex(s => s.QuoteNumber).IsUnique();
        b.Entity<Layaway>().HasIndex(l => l.LayawayNumber).IsUnique();
        b.Entity<MoMoTransaction>().HasIndex(m => m.TransactionId).IsUnique();

        b.Entity<InventoryItem>()
            .HasIndex(i => new { i.StoreId, i.ProductId }).IsUnique();

        // Stock transfers reference Store twice — disable cascade to prevent multi-path on SQL Server
        b.Entity<StockTransfer>()
            .HasOne(t => t.FromStore).WithMany().HasForeignKey(t => t.FromStoreId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockTransfer>()
            .HasOne(t => t.ToStore).WithMany().HasForeignKey(t => t.ToStoreId)
            .OnDelete(DeleteBehavior.Restrict);

        // ---- Multi-tenant global query filter ----
        // Applied to every entity implementing ITenantScoped. When TenantContext is
        // injected, queries auto-restrict to the current tenant. When Enabled is
        // false, the filter trivially matches everything (TenantId == TenantId).
        b.Entity<Customer>().HasQueryFilter(c => !TenantEnabled() || c.TenantId == CurrentTenantId());
        b.Entity<Store>()   .HasQueryFilter(s => !TenantEnabled() || s.TenantId == CurrentTenantId());
        b.Entity<Product>() .HasQueryFilter(p => !TenantEnabled() || p.TenantId == CurrentTenantId());
        b.Entity<Supplier>().HasQueryFilter(s => !TenantEnabled() || s.TenantId == CurrentTenantId());

        foreach (var prop in b.Model.GetEntityTypes()
                    .SelectMany(t => t.GetProperties())
                    .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            prop.SetPrecision(18);
            prop.SetScale(2);
        }
    }

    /// <summary>Auto-fill TenantId on insert for ITenantScoped entities.</summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_tenant != null && _tenant.Enabled)
        {
            foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
            {
                if (entry.State == EntityState.Added && entry.Entity.TenantId == 0)
                    entry.Entity.TenantId = _tenant.TenantId;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    // EF Core requires the filter expression to use methods that can be translated.
    // We hide TenantContext access behind these helpers so the expression-tree compiler
    // treats them as constants captured at filter-compile time.
    private bool TenantEnabled() => _tenant?.Enabled ?? false;
    private int  CurrentTenantId() => _tenant?.TenantId ?? 0;
}
