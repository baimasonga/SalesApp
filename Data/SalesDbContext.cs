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

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>().HasIndex(p => p.Sku).IsUnique();
        b.Entity<Sale>().HasIndex(s => s.InvoiceNumber).IsUnique();

        b.Entity<InventoryItem>()
            .HasIndex(i => new { i.StoreId, i.ProductId }).IsUnique();

        foreach (var prop in b.Model.GetEntityTypes()
                    .SelectMany(t => t.GetProperties())
                    .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            prop.SetPrecision(18);
            prop.SetScale(2);
        }
    }
}
