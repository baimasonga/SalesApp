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
            new Product { Sku = "RIC-50", Barcode = "5901234123451", Name = "Rice (50kg bag)", Category = "Foodstuff", UnitPrice = 850m, CostPrice = 720m, Unit = "bag" },
            new Product { Sku = "OIL-5L", Barcode = "5901234123452", Name = "Vegetable Oil 5L", Category = "Foodstuff", UnitPrice = 220m, CostPrice = 180m, Unit = "btl" },
            new Product { Sku = "SUG-1K", Barcode = "5901234123453", Name = "Sugar 1kg", Category = "Foodstuff", UnitPrice = 28m, CostPrice = 22m, Unit = "pkt" },
            new Product { Sku = "CMT-50", Barcode = "5901234123454", Name = "Cement 50kg", Category = "Building", UnitPrice = 145m, CostPrice = 125m, Unit = "bag" },
            new Product { Sku = "ZNC-3M", Barcode = "5901234123455", Name = "Zinc Sheet 3m", Category = "Building", UnitPrice = 95m, CostPrice = 78m, Unit = "sht" },
            new Product { Sku = "PHN-A12", Barcode = "5901234123456", Name = "Smartphone A12", Category = "Electronics", UnitPrice = 1450m, CostPrice = 1200m, Unit = "pcs" },
            new Product { Sku = "AIR-100", Barcode = "5901234123457", Name = "Airtime Voucher 100", Category = "Telecom", UnitPrice = 100m, CostPrice = 92m, Unit = "pcs" },
            new Product { Sku = "SOA-BAR", Barcode = "5901234123458", Name = "Bar Soap", Category = "Household", UnitPrice = 15m, CostPrice = 11m, Unit = "pcs" },
            new Product { Sku = "WTR-50", Barcode = "5901234123459", Name = "Bottled Water (case)", Category = "Beverage", UnitPrice = 65m, CostPrice = 50m, Unit = "case" },
            new Product { Sku = "FAB-YD", Barcode = "5901234123460", Name = "African Print Fabric", Category = "Textile", UnitPrice = 180m, CostPrice = 140m, Unit = "yd" }
        };
        db.Products.AddRange(products);

        var suppliers = new[]
        {
            new Supplier { Name = "Sierra Wholesale Foods Ltd", Phone = "+232 76 900 100", Email = "sales@swfoods.sl", Country = "Sierra Leone", Address = "Kissy Road, Freetown" },
            new Supplier { Name = "Atlantic Imports Ltd", Phone = "+232 77 900 200", Email = "info@atlanticimports.sl", Country = "Sierra Leone", Address = "Wallace Johnson St, Freetown" },
            new Supplier { Name = "Guangzhou Trading Co.", Phone = "+86 20 8888 0000", Email = "export@gz-trading.cn", Country = "China" }
        };
        db.Suppliers.AddRange(suppliers);

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
                    CostPrice = p.CostPrice,
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

        // Tenant (single demo tenant)
        db.Tenants.Add(new Tenant { Name = "Demo Business SL", Subdomain = "demo", Phone = "+232 76 000 000", Email = "info@demo.sl", DefaultCurrency = "NLe" });

        // Employees
        db.Employees.AddRange(
            new Employee { FullName = "Aminata Kamara", Role = "Manager", Phone = "+232 76 100 100", Email = "aminata@demo.sl", StoreId = stores[0].Id, MonthlySalary = 4500m, CommissionRate = 0.015m },
            new Employee { FullName = "Mohamed Sesay", Role = "Cashier", Phone = "+232 77 200 200", StoreId = stores[1].Id, MonthlySalary = 2800m, CommissionRate = 0.020m },
            new Employee { FullName = "Fatmata Conteh", Role = "Cashier", Phone = "+232 78 300 300", StoreId = stores[2].Id, MonthlySalary = 2800m, CommissionRate = 0.020m },
            new Employee { FullName = "Joseph Mansaray", Role = "Stock Clerk", Phone = "+232 99 400 400", StoreId = stores[0].Id, MonthlySalary = 2300m, CommissionRate = 0m }
        );

        // A couple of expenses
        db.Expenses.AddRange(
            new Expense { StoreId = stores[0].Id, Description = "Generator fuel (April)", Category = "Utilities", Amount = 350m, PaidTo = "NP Sierra Leone", RecordedBy = "Aminata", Date = DateTime.UtcNow.AddDays(-3) },
            new Expense { StoreId = stores[1].Id, Description = "Bo branch monthly rent", Category = "Rent", Amount = 1500m, PaidTo = "Landlord", RecordedBy = "Mohamed", Date = DateTime.UtcNow.AddDays(-15) }
        );

        // One sample layaway
        db.Layaways.Add(new Layaway
        {
            LayawayNumber = $"LAY-{DateTime.UtcNow:yyyyMMdd}-001",
            CustomerId = customers[0].Id,
            StoreId = stores[0].Id,
            Description = "Smartphone A12 layaway",
            TotalAmount = 1450m,
            AmountPaid = 500m,
            Status = LayawayStatus.Active,
            Payments = new List<LayawayPayment>
            {
                new() { Date = DateTime.UtcNow.AddDays(-7), Amount = 300m, Method = PaymentMethod.Cash, ReceivedBy = "Aminata" },
                new() { Date = DateTime.UtcNow.AddDays(-1), Amount = 200m, Method = PaymentMethod.MobileMoney, Reference = "OM-23890", ReceivedBy = "Aminata" }
            }
        });

        // Sales targets for current month
        var now = DateTime.UtcNow;
        db.SalesTargets.AddRange(
            new SalesTarget { StoreId = null, Year = now.Year, Month = now.Month, TargetAmount = 250000m, Notes = "Company-wide" },
            new SalesTarget { StoreId = stores[0].Id, Year = now.Year, Month = now.Month, TargetAmount = 120000m },
            new SalesTarget { StoreId = stores[1].Id, Year = now.Year, Month = now.Month, TargetAmount = 80000m },
            new SalesTarget { StoreId = stores[2].Id, Year = now.Year, Month = now.Month, TargetAmount = 50000m }
        );

        db.Interactions.AddRange(
            new Interaction { CustomerId = customers[0].Id, Type = InteractionType.Call, Subject = "Bulk rice quote", Details = "Requested pricing for 200 bags.", Owner = "Aminata", FollowUpDate = DateTime.UtcNow.AddDays(3) },
            new Interaction { CustomerId = customers[2].Id, Type = InteractionType.Meeting, Subject = "Cement supply contract", Details = "Discussed monthly supply for project.", Owner = "Mohamed", FollowUpDate = DateTime.UtcNow.AddDays(7) },
            new Interaction { CustomerId = customers[1].Id, Type = InteractionType.WhatsApp, Subject = "Restock confirmation", Details = "Confirmed delivery for next Tuesday.", Owner = "Fatmata" }
        );

        await db.SaveChangesAsync();
    }
}
