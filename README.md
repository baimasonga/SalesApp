# Salone Sales — ASP.NET Core Blazor

A sales, inventory, CRM and analytics platform tailored for the Sierra Leone
business community. Built with **ASP.NET Core 8 Blazor Web App (Interactive
Server)** and **EF Core**.

## Modules

- **Dashboard** — KPIs (today/month revenue, orders, AOV, low-stock count, customers) plus charts.
- **Sales** — list, create with multi-line cart, GST 15%, cancel & restock.
- **Stores** — multi-store support (Freetown, Bo, Kenema, etc.) with district picker.
- **Products** — SKUs, cost & selling price, categories.
- **Inventory** — per-store stock levels, low-stock highlighting, quick adjust (+/−).
- **CRM** — customer directory (search, segments: Retail/Wholesale/Corporate/Government/NGO), interaction logging (Call, WhatsApp, Visit, etc.), follow-ups, lifetime value.
- **Reports & Analytics** — daily revenue, top products, sales by store, payment-method mix.

Currency formatted as **NLe** (New Leone). Sample data uses Sierra Leonean
districts, names and product mix (rice, oil, cement, zinc sheets, airtime,
African print fabric, etc.).

## Database

EF Core is configured in `Program.cs` with two providers selectable via
`appsettings.json`:

```json
"Database": { "Provider": "InMemory" },
"ConnectionStrings": {
  "Default": "Server=.\\SQLEXPRESS;Database=SalesAppDb;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

- **InMemory** (default) — runs out-of-the-box, seeds sample data on startup.
- **SqlServer** — set `"Provider": "SqlServer"`. Targets `.\SQLEXPRESS` by default. The seeder calls `EnsureCreated`, so the schema is created automatically on first run.

To switch to SQL Express:

1. Install SQL Server Express + enable the `SQLEXPRESS` instance.
2. Edit `appsettings.json`: `"Provider": "SqlServer"`.
3. Adjust the connection string if needed.
4. `dotnet run` — schema is created and sample data seeded.

For production migrations, run:

```
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Run

```
dotnet run
```

Then open http://localhost:5099 (or whatever port Kestrel reports).

## Project layout

```
Models/        Domain entities
Data/          SalesDbContext + DbSeeder
Services/      SalesService, InventoryService, CrmService, AnalyticsService
Components/
  Pages/       Home (dashboard), Sales, NewSale, Stores, Products, Inventory,
               Customers, CustomerDetail, Reports
  Shared/      KpiCard, BarChart
  Layout/      MainLayout, NavMenu
wwwroot/
  salone.css   Custom theme (Sierra Leone flag accent colors)
```
