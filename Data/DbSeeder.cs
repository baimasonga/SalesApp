using Microsoft.EntityFrameworkCore;
using SalesApp.Models;

namespace SalesApp.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(SalesDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Stores.AnyAsync()) return;

        var stores = new[]
        {
            new Store { Name = "Freetown Main", Location = "Sani Abacha Street", District = "Western Area Urban", Phone = "+232 76 100 100", ManagerName = "Aminata Kamara" },
            new Store { Name = "Bo Branch", Location = "Fenton Road", District = "Bo", Phone = "+232 77 200 200", ManagerName = "Mohamed Sesay" },
            new Store { Name = "Kenema Outlet", Location = "Hangha Road", District = "Kenema", Phone = "+232 78 300 300", ManagerName = "Fatmata Conteh" }
        };
        db.Stores.AddRange(stores);

        var products = new[]
        {
            new Product { Sku = "RIC-50", Name = "Rice (50kg bag)", Category = "Foodstuff", UnitPrice = 850m, CostPrice = 720m, Unit = "bag" },
            new Product { Sku = "OIL-5L", Name = "Vegetable Oil 5L", Category = "Foodstuff", UnitPrice = 220m, CostPrice = 180m, Unit = "btl" },
            new Product { Sku = "SUG-1K", Name = "Sugar 1kg", Category = "Foodstuff", UnitPrice = 28m, CostPrice = 22m, Unit = "pkt" },
            new Product { Sku = "CMT-50", Name = "Cement 50kg", Category = "Building", UnitPrice = 145m, CostPrice = 125m, Unit = "bag" },
            new Product { Sku = "ZNC-3M", Name = "Zinc Sheet 3m", Category = "Building", UnitPrice = 95m, CostPrice = 78m, Unit = "sht" },
            new Product { Sku = "PHN-A12", Name = "Smartphone A12", Category = "Electronics", UnitPrice = 1450m, CostPrice = 1200m, Unit = "pcs" },
            new Product { Sku = "AIR-100", Name = "Airtime Voucher 100", Category = "Telecom", UnitPrice = 100m, CostPrice = 92m, Unit = "pcs" },
            new Product { Sku = "SOA-BAR", Name = "Bar Soap", Category = "Household", UnitPrice = 15m, CostPrice = 11m, Unit = "pcs" },
            new Product { Sku = "WTR-50", Name = "Bottled Water (case)", Category = "Beverage", UnitPrice = 65m, CostPrice = 50m, Unit = "case" },
            new Product { Sku = "FAB-YD", Name = "African Print Fabric", Category = "Textile", UnitPrice = 180m, CostPrice = 140m, Unit = "yd" }
        };
        db.Products.AddRange(products);

        var customers = new[]
        {
            new Customer { FullName = "Ibrahim Bangura", BusinessName = "Bangura Trading Co.", Phone = "+232 76 555 010", Email = "ibrahim@bangura.sl", District = "Western Area Urban", Segment = CustomerSegment.Wholesale },
            new Customer { FullName = "Hawa Jalloh", BusinessName = "Hawa Mini-Mart", Phone = "+232 77 555 020", District = "Bo", Segment = CustomerSegment.Retail },
            new Customer { FullName = "Sorie Koroma", BusinessName = "Koroma Construction Ltd", Phone = "+232 78 555 030", Email = "info@koroma-build.sl", District = "Kenema", Segment = CustomerSegment.Corporate },
            new Customer { FullName = "Isatu Turay", Phone = "+232 99 555 040", District = "Western Area Urban", Segment = CustomerSegment.Retail },
            new Customer { FullName = "Ministry of Works", BusinessName = "Govt of Sierra Leone", Phone = "+232 22 555 050", Email = "procurement@mow.gov.sl", District = "Western Area Urban", Segment = CustomerSegment.Government },
            new Customer { FullName = "Concern Worldwide SL", BusinessName = "Concern WW", Phone = "+232 76 555 060", District = "Makeni", Segment = CustomerSegment.NGO }
        };
        db.Customers.AddRange(customers);

        await db.SaveChangesAsync();

        var inventory = new List<InventoryItem>();
        var rng = new Random(42);
        foreach (var s in stores)
            foreach (var p in products)
                inventory.Add(new InventoryItem { StoreId = s.Id, ProductId = p.Id, QuantityOnHand = rng.Next(3, 80), ReorderLevel = 8 });
        db.InventoryItems.AddRange(inventory);

        var sales = new List<Sale>();
        for (int i = 0; i < 40; i++)
        {
            var store = stores[rng.Next(stores.Length)];
            var cust = customers[rng.Next(customers.Length)];
            var date = DateTime.UtcNow.AddDays(-rng.Next(0, 60)).AddHours(-rng.Next(0, 12));
            var sale = new Sale
            {
                InvoiceNumber = $"INV-{date:yyyyMMdd}-{1000 + i}",
                StoreId = store.Id,
                CustomerId = cust.Id,
                SaleDate = date,
                PaymentMethod = (PaymentMethod)rng.Next(0, 5),
                Status = SaleStatus.Completed,
                CashierName = new[] { "Aminata", "Mohamed", "Fatmata", "Joseph" }[rng.Next(4)]
            };
            int lineCount = rng.Next(1, 5);
            for (int l = 0; l < lineCount; l++)
            {
                var p = products[rng.Next(products.Length)];
                var qty = rng.Next(1, 6);
                var line = new SaleItem
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    Quantity = qty,
                    UnitPrice = p.UnitPrice,
                    LineTotal = qty * p.UnitPrice
                };
                sale.Items.Add(line);
            }
            sale.Subtotal = sale.Items.Sum(x => x.LineTotal);
            sale.Tax = Math.Round(sale.Subtotal * 0.15m, 2);
            sale.Discount = 0;
            sale.Total = sale.Subtotal + sale.Tax - sale.Discount;
            sales.Add(sale);
        }
        db.Sales.AddRange(sales);

        db.Interactions.AddRange(
            new Interaction { CustomerId = customers[0].Id, Type = InteractionType.Call, Subject = "Bulk rice quote", Details = "Requested pricing for 200 bags.", Owner = "Aminata", FollowUpDate = DateTime.UtcNow.AddDays(3) },
            new Interaction { CustomerId = customers[2].Id, Type = InteractionType.Meeting, Subject = "Cement supply contract", Details = "Discussed monthly supply for project.", Owner = "Mohamed", FollowUpDate = DateTime.UtcNow.AddDays(7) },
            new Interaction { CustomerId = customers[1].Id, Type = InteractionType.WhatsApp, Subject = "Restock confirmation", Details = "Confirmed delivery for next Tuesday.", Owner = "Fatmata" }
        );

        await db.SaveChangesAsync();
    }
}
