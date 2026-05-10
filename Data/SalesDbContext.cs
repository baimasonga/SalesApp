using Microsoft.EntityFrameworkCore;
using SalesApp.Models;

namespace SalesApp.Data;

public class SalesDbContext : DbContext
{
    public SalesDbContext(DbContextOptions<SalesDbContext> options) : base(options) { }

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

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>().HasIndex(p => p.Sku).IsUnique();
        b.Entity<Sale>().HasIndex(s => s.InvoiceNumber).IsUnique();
        b.Entity<PurchaseOrder>().HasIndex(s => s.PoNumber).IsUnique();
        b.Entity<StockTransfer>().HasIndex(s => s.TransferNumber).IsUnique();
        b.Entity<Quotation>().HasIndex(s => s.QuoteNumber).IsUnique();
        b.Entity<Layaway>().HasIndex(l => l.LayawayNumber).IsUnique();

        b.Entity<InventoryItem>()
            .HasIndex(i => new { i.StoreId, i.ProductId }).IsUnique();

        // Stock transfers reference Store twice — disable cascade to prevent multi-path on SQL Server
        b.Entity<StockTransfer>()
            .HasOne(t => t.FromStore).WithMany().HasForeignKey(t => t.FromStoreId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockTransfer>()
            .HasOne(t => t.ToStore).WithMany().HasForeignKey(t => t.ToStoreId)
            .OnDelete(DeleteBehavior.Restrict);

        foreach (var prop in b.Model.GetEntityTypes()
                    .SelectMany(t => t.GetProperties())
                    .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            prop.SetPrecision(18);
            prop.SetScale(2);
        }
    }
}
