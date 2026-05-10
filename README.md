# Salone Sales 🇸🇱

A complete sales, inventory, CRM, finance, HR and analytics platform built for the
Sierra Leone business community. Built with **ASP.NET Core 8 Blazor Web App**
(Interactive Server) + **Entity Framework Core**.

---

## Modules

| Module | What it does |
|---|---|
| **Dashboard** | Greeting hero, 7 KPIs, daily revenue line chart, top products, sales by store, payment mix, follow-ups, low-stock alerts |
| **Sales** | List with date/store filters, new-sale cart with barcode/SKU input, line discounts, GST 15%, NLe/USD currency, cancel/refund, A4 invoice + 80mm thermal receipt |
| **Quotations** | Draft → Sent → Accepted → **Convert to Sale** |
| **Layaways** | Installment payments (deposit + add payments), auto-complete on full payment |
| **Inventory** | Multi-store stock, low-stock alerts, reorder report, stock-take with variance, stock transfers between stores |
| **Procurement** | Suppliers + purchase orders (Draft/Submitted/Received), receive auto-credits stock |
| **CRM** | Customer directory, segments, search, interaction log (Call/SMS/WhatsApp/Visit/Note), follow-ups, loyalty points, lifetime value |
| **Finance & HR** | Expenses with category breakdown, employees + commission rates, shifts (clock in/out), commission report, P&L statement |
| **Reports** | Date-filtered reports, 4 charts (line/bar/donut), profit margin per product, P&L, sales/NRA tax CSV exports |
| **Sales Targets** | Per-store + company-wide monthly targets with progress bars |
| **Audit Log** | Last 200 events: every sale, refund, transfer, PO, layaway, employee change |
| **Multi-tenant** | `Tenant` entity + `TenantContext` scaffolding (single-tenant in demo, ready to enable) |
| **Mobile Money** | `/webhook/momo` HMAC-verified endpoint that confirms sales by invoice number |
| **SMS** | `INotificationService` with `AfricellSmsService` implementation (graceful demo fallback) |
| **PWA** | Manifest + service worker — installable on Android/desktop, app-shell caching |

---

## Quick start (demo mode)

Default mode uses **EF Core In-Memory** so it runs without a database:

```bash
dotnet run
```

Open http://localhost:5099. Default seeded user: anyone — set your name on `/login`.

### Keyboard shortcuts
- `N` — New sale
- `D` — Dashboard
- `R` — Reports
- `?` — Show shortcut help

### Toggle dark mode
Top bar moon/sun icon. Persists per browser via localStorage.

---

## Switch to SQL Express

```powershell
# 1. Install SQL Server Express + sqlcmd  (one-time)
# 2. From the project root:
powershell -ExecutionPolicy Bypass -File scripts\setup-sqlexpress.ps1
```

The script flips `appsettings.json` `Database:Provider` to `"SqlServer"`,
adds an `InitialCreate` migration and creates `SalesAppDb` on `.\SQLEXPRESS`.

### Daily backup
Schedule via Task Scheduler:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\backup-db.ps1 -BackupDir "C:\SalesAppBackups"
```

---

## Enable real authentication

In `appsettings.json`:
```json
"Auth": { "Enabled": true }
```

On first run an admin is seeded:
- Email: `admin@demo.sl`
- Password: `Admin@1234`

Roles: **Admin**, **Manager**, **Cashier**. Authorization policies:
`ManagerOrAdmin`, `AdminOnly`.

---

## Mobile Money webhook (Orange Money / Afrimoney)

Configure your operator aggregator to POST confirmations to:
```
POST /webhook/momo
Content-Type: application/json
X-Signature: <hex HMAC-SHA256 of body using MoMo:WebhookSecret>

{
  "merchantReference": "INV-20260509-1024",
  "transactionId": "OM-9988776655",
  "amount": 1450.00,
  "operator": "OrangeMoney",
  "payerPhone": "+23276555010"
}
```
The endpoint validates the HMAC, finds the sale by `InvoiceNumber`, sets the
payment method and txn ref, then writes an audit entry.

---

## SMS notifications (Africell)

Set in `appsettings.json`:
```json
"SMS": {
  "Provider": "Africell",
  "ApiUrl": "https://your-aggregator/api/v1/messages",
  "ApiKey": "...",
  "SenderId": "SaloneSales"
}
```
With no `ApiKey`, the service logs to console (demo mode) and returns success.

Test:
```bash
curl -X POST "http://localhost:5099/api/test/sms?phone=+23276555010&message=Hello"
```

---

## Docker deployment

```bash
docker compose up -d
# App → http://localhost:8080
# DB  → localhost:1433 (sa / Salone#Sales1234!)
```

The compose file runs **SQL Server 2022 Express** in a sibling container with
persistent volume `salone_db_data`.

---

## Project layout

```
Components/
  Layout/           MainLayout, NavMenu, EmptyLayout
  Pages/            17 routable pages (Home, Sales, Inventory, CRM, Finance...)
  Shared/           KpiCard, BarChart, ChartJs (Chart.js wrapper), ToastContainer
Data/
  SalesDbContext    Domain entities
  Identity/         ApplicationUser + IdentityAppDbContext (auth)
  DbSeeder          Sample SL data
  Migrations/       (created by setup-sqlexpress.ps1)
Services/
  Notifications/    INotificationService, AfricellSmsService, MobileMoneyService
  *.cs              SalesService, InventoryService, CrmService, AnalyticsService,
                    PurchaseOrderService, StockTransferService, QuotationService,
                    TargetService, ExportService, AuditService, ExpenseService,
                    EmployeeService, LayawayService, ToastService, ThemeService,
                    NavDrawerState, LayoutState, TenantContext, CurrentUserService
wwwroot/
  salone.css        Design system (light + dark mode)
  app.js            JS interop: theme, charts, shortcuts, downloads, SW reg
  service-worker.js PWA app-shell caching
  manifest.webmanifest
scripts/
  setup-sqlexpress.ps1
  backup-db.ps1
Dockerfile
docker-compose.yml
```

---

## Tech notes

- **Charts**: Chart.js v4 from CDN, JS-side instance map keyed by string ID (no IJSObjectReference round-trip needed)
- **PWA**: service worker bypasses `_blazor`, `_framework`, `/export/*` paths so SignalR + downloads still work
- **Dark mode**: applied via `<html data-theme="dark">`, persisted in localStorage, set before first paint to avoid flash
- **Mobile drawer**: under 900px the sidebar fixes-position and slides in via the hamburger button
- **Multi-tenant**: `TenantContext` is scoped per circuit; entities can implement `ITenantScoped` and a global query filter applied in `OnModelCreating` once you flip the switch
