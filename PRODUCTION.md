# Salone Sales — Production Deployment Runbook

This is the operational guide for taking Salone Sales from dev demo to a real Sierra Leone business deployment.

---

## 0. Pre-flight checklist

- [ ] **SQL Server Express** installed and `SQLEXPRESS` instance running
- [ ] **.NET 8 runtime** installed (or Docker engine if containerising)
- [ ] **HTTPS certificate** for your domain (Let's Encrypt or commercial)
- [ ] **Backup destination** ready (file share, S3-compatible bucket, OneDrive sync folder)
- [ ] **NRA TIN** for the business (you'll enter it in Settings)
- [ ] **Africell SMS** account + API key (optional)
- [ ] **Orange Money / Afrimoney** merchant account + webhook secret (optional)
- [ ] **WhatsApp Business** Meta Cloud API access token (optional)

---

## 1. Initial install

```powershell
# Clone & build
git clone https://github.com/.../SalesApp.git
cd SalesApp
dotnet publish -c Release -o ./out

# One-time DB setup (creates SalesAppDb on .\SQLEXPRESS)
powershell -ExecutionPolicy Bypass -File scripts/setup-sqlexpress.ps1
```

## 2. Configure for production

Copy `appsettings.Production.json` and fill in real values. Critical fields:

```jsonc
{
  "Database":  { "Provider": "SqlServer" },
  "Auth":      { "Enabled":  true },                    // ENFORCES real ASP.NET Identity
  "Tenancy":   { "Enabled":  false },                   // flip true if SaaS
  "SMS":       { "ApiUrl":   "https://...", "ApiKey": "..." },
  "MoMo":      { "WebhookSecret": "32+ char random secret" },
  "WhatsApp":  { "AccessToken": "...", "PhoneNumberId": "..." }
}
```

**Never commit real secrets.** Use environment variables or Windows secrets store:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:SMS__ApiKey            = "your-real-africell-key"
$env:MoMo__WebhookSecret    = "your-real-momo-secret"
$env:WhatsApp__AccessToken  = "your-real-meta-token"
$env:ConnectionStrings__Default = "Server=.\SQLEXPRESS;Database=SalesAppDb;Trusted_Connection=True;TrustServerCertificate=True;"
```

## 3. First-run setup

```powershell
# Start the service / app
dotnet ./out/SalesApp.dll --urls=https://*:443

# In another shell, smoke-test
powershell -ExecutionPolicy Bypass -File scripts/smoke-test.ps1 -BaseUrl https://your-domain.sl
```

Then in the browser:
1. Sign in as the seeded `admin@demo.sl` / `Admin@1234` (change immediately)
2. Open **/settings** → fill in real Business Name, NRA TIN, Address, Phone, Email
3. Open **/admin/sms** → send a test SMS to confirm the gateway works
4. Open **/admin/momo** → simulate a webhook to validate the secret
5. Open **/admin/tenants** → create real tenant rows if multi-tenant

## 4. Daily ops

### Backups (Windows Task Scheduler)
```powershell
# Daily at 2 AM
powershell -ExecutionPolicy Bypass -File scripts/backup-db.ps1 -BackupDir "D:\SalesAppBackups"
```
Retains last 30 daily backups. Off-site copy via OneDrive/Dropbox/rclone is your responsibility.

### Log files
- App logs: `logs/salone-YYYYMMDD.log` (Serilog, daily rolling, 14-day retention)
- Audit log: `/audit` page or `/export/audit.csv?from=&to=`
- Web server: `%TEMP%\stdout-*.log` if hosted in IIS

### Monitoring endpoints
| URL | Use |
|---|---|
| `GET /health/live`    | Process is up. Hit from your uptime monitor every 30s. |
| `GET /health`         | DB-reachable. Hit every 60s. Returns JSON with per-check status. |
| `GET /api/version`    | Build/commit info for support tickets. |

### Service restart
If you've installed as a Windows service:
```powershell
Restart-Service SaloneSales
```

## 5. NRA monthly return

On the 1st of each month:
1. Open `/reports`
2. Set From/To to the previous month
3. Click **NRA Tax CSV**
4. Submit the file via your NRA portal upload

## 6. Updating the app

```powershell
# Stop service
Stop-Service SaloneSales

# Pull and rebuild
git pull
dotnet publish -c Release -o ./out

# Apply any new EF migrations (production-safe)
dotnet ef database update --connection "Server=.\SQLEXPRESS;Database=SalesAppDb;Trusted_Connection=True;TrustServerCertificate=True;"

# Restart service + smoke test
Start-Service SaloneSales
powershell -ExecutionPolicy Bypass -File scripts/smoke-test.ps1 -BaseUrl https://your-domain.sl
```

If the smoke test fails: `Stop-Service SaloneSales`, restore the previous `out/` from your backup, restart.

## 7. Settings that take effect at runtime

Edits in **/settings** apply immediately — no restart needed:

| Setting | Affects |
|---|---|
| `Business.*` (name, TIN, address, phone, email) | Invoice/receipt headers; NRA tax export |
| `Business.Currency` | Default currency for new sales |
| `Tax.GstRate` | All new sales + quotations |
| `Sales.DefaultDiscountCap` | Server-side enforcement on every new sale |
| `Sales.RefundWindowDays` | Server-side enforcement on refund |
| `Inventory.DefaultReorderLevel` | New inventory rows |
| `Loyalty.PointsPerNLe` | Points accrual & reversal on every NLe sale |
| `Receipt.HeaderText` / `FooterText` | Both A4 and 80mm receipts |
| `Audit.RetentionDays` | Used by Audit page's purge action |
| `SMS.*`, `MoMo.*`, `WhatsApp.*` | Read fresh on every send (DB overrides appsettings) |

## 8. Common issues

**"Cannot connect to database"** → SQL Express service not running. `Start-Service MSSQL$SQLEXPRESS`.

**Rate-limit 429 in production** → Legitimate aggregator hitting webhook too fast. Raise the limit in `Program.cs` `momo-webhook` policy.

**Cashier sees "Access denied"** → Their role is `Cashier`. Sign in as Admin via `/login`, then in real Identity setup add them to the `Manager` role via the Identity DB.

**Cached SW serving old assets** → Bump `service-worker.js` cache version (`v8` → `v9`) and hard-refresh.

**SaveChanges concurrency conflict** → Two cashiers edited the same row. Reload and retry — by design (concurrency tokens prevent overselling).

---

## Security checklist before going live

- [ ] `Auth:Enabled = true` in production config
- [ ] Default admin password changed
- [ ] `MoMo:WebhookSecret` set to a strong 32+ char value
- [ ] HTTPS enforced (`UseHttpsRedirection`)
- [ ] Firewall: only ports 80/443 open externally
- [ ] DB credentials in env vars, not in source
- [ ] Backup destination tested (do a restore drill once)
- [ ] Log retention conforms to NRA/business records-keeping rules (typically ≥ 6 years)
