# Salone Sales — Windows Desktop Installation

This document covers shipping Salone Sales as **shrink-wrap software** for a single Windows PC (typical shop) or LAN-connected workstations (multi-station shop).

For server / SQL Express deployments, see [PRODUCTION.md](PRODUCTION.md).

---

## What gets installed

| Component | Where | Purpose |
|---|---|---|
| Application binaries | `C:\Program Files\SaloneSales\` | Self-contained — no .NET runtime needed |
| Database | `C:\Program Files\SaloneSales\data\salone.db` | SQLite — single file, easy backup |
| Logs | `C:\Program Files\SaloneSales\logs\` | Rolling daily, 14-day retention |
| Windows Service | `SaloneSales` | Auto-starts on boot, restarts on crash |
| Firewall rule | "Salone Sales (Port 5099)" | TCP 5099 inbound on Private + Domain profiles only |
| Shortcuts | Start Menu + Desktop | Opens `http://localhost:5099` in the default browser |

System requirements: **Windows 10 / 11 / Server 2016+**, x64, 200 MB free disk, 512 MB RAM.

No internet required for daily use.

---

## Building the installer (you, the vendor)

### 1. Publish a self-contained Windows build

```powershell
cd <repo-root>
powershell -ExecutionPolicy Bypass -File scripts\publish-win.ps1
# Output: publish\win-x64\ (≈ 120 MB, includes the .NET runtime)
```

### 2. Build the installer

Install [Inno Setup 6.x](https://jrsoftware.org/isinfo.php) (free). Then:

```powershell
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\SalesApp.iss
# Output: installer\Output\SaloneSales-Setup-1.0.0.exe (≈ 60 MB compressed)
```

This `.exe` is what you give customers. They double-click it; UAC asks for admin; install completes; browser opens to the app.

### 3. Code-sign the installer (recommended for commercial release)

Without a code signing cert, Windows SmartScreen shows a warning. Get an OV/EV code signing cert from DigiCert/Sectigo/SSL.com (~$200–600/year).

```powershell
signtool sign /f mycert.pfx /p <password> /tr http://timestamp.digicert.com /td sha256 /fd sha256 `
    installer\Output\SaloneSales-Setup-1.0.0.exe
```

---

## Customer install experience

1. Customer downloads or receives the installer (`SaloneSales-Setup-1.0.0.exe`).
2. Double-click. UAC prompts for admin permission.
3. Accept the EULA (loaded from `LICENSE.txt`).
4. Choose install location (default `C:\Program Files\SaloneSales`).
5. Choose desktop / start menu shortcuts (default both).
6. Click Install — takes ~30 seconds.
7. Click Finish — the browser opens `http://localhost:5099` automatically.
8. First-run setup: app is in **30-day Trial mode**; banner shows trial countdown.

---

## License activation (customer)

The trial lasts 30 days. To go past that, the customer needs a paid license key.

### Sales flow

1. Customer opens `/license` page, copies their **machine fingerprint** (e.g. `a1b2c3d4e5f6g7h8`)
2. Sends it to `sales@salonesales.sl` with payment
3. You (the vendor) generate a key via:

```csharp
var svc = new LicenseService(...);
var key = svc.MintKey(
    licensedTo: "Bangura Trading Co.",
    expiresUtc: DateTime.UtcNow.AddYears(1),
    tier: LicenseTier.Business,
    features: "core,sms,whatsapp,momo");
// Email the resulting "SALONE-..." string to the customer
```

4. Customer pastes the key on `/license` → clicks Activate → done.

The key is HMAC-signed using the secret in `License:SigningSecret` (config). **Set this to a strong random value before shipping** — keys minted with one secret won't validate against another.

---

## Multi-station deployment (small chain)

Install the app on **one** PC (the shopkeeper's machine). Other workstations access it via browser at `http://<shop-pc-name>:5099` over the LAN.

```powershell
# On the shop PC, find its name and IP:
hostname
ipconfig | findstr IPv4
```

The firewall rule installed by setup allows this on Private and Domain network profiles (not on Public — safe for cafés / open WiFi).

For real multi-tenant SaaS deployment, switch `Database:Provider` to `SqlServer` and follow [PRODUCTION.md](PRODUCTION.md) instead.

---

## Daily operations

### Backup the database

The entire database is one SQLite file: `C:\Program Files\SaloneSales\data\salone.db`.

Add to Windows Task Scheduler (run daily at 2 AM):

```powershell
Copy-Item "C:\Program Files\SaloneSales\data\salone.db" `
          "D:\Backups\salone-$(Get-Date -Format yyyyMMdd).db"
# Then optionally upload to OneDrive / Dropbox / rclone
```

To restore: stop the service, copy the backup file back, start the service.

```powershell
Stop-Service SaloneSales
Copy-Item "D:\Backups\salone-20260512.db" "C:\Program Files\SaloneSales\data\salone.db" -Force
Start-Service SaloneSales
```

### Service management

```powershell
Get-Service SaloneSales              # status
Restart-Service SaloneSales          # restart after config change
Stop-Service SaloneSales             # before manual backup/restore
sc.exe config SaloneSales start= demand   # disable autostart
```

### Log files

`C:\Program Files\SaloneSales\logs\salone-YYYYMMDD.log` — Serilog rolling daily, kept 14 days.

### Updates

1. Stop the service: `Stop-Service SaloneSales`
2. Run the new installer — it'll preserve `data\` and `logs\`
3. Service auto-starts after install

---

## Uninstall

Control Panel → Programs → uninstall **Salone Sales** — or run the bundled `uninstall-service.ps1`. By default the `data\` folder is **preserved** so customer data survives reinstall. To wipe everything, manually delete `C:\Program Files\SaloneSales`.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| "Service did not start" | Check `logs\salone-YYYYMMDD.log` and Windows Event Viewer → Application |
| Browser shows "Cannot reach" | `Get-Service SaloneSales` — should be `Running`. If not, `Start-Service SaloneSales` |
| Trial banner won't go away | `/license` → paste paid key. Confirm signing secret matches the vendor's `License:SigningSecret` |
| Slow on first request | First call after boot warms up the EF model + Serilog file. Normal — subsequent calls are sub-100ms |
| Can't reach from another PC | Hostname/IP correct? Firewall profile is Private (not Public)? `Get-NetFirewallRule -DisplayName "Salone*"` |
| "Database is locked" (SQLite) | Two processes wrote at the same time — shouldn't happen with the service. If it does, restart service |

---

## Files in the installer

```
SaloneSales-Setup-1.0.0.exe         (the customer-facing installer)
├── SalesApp.exe                    (single executable, no other binaries to copy)
├── *.dll                           (~150 MB of .NET runtime + dependencies)
├── wwwroot\                        (CSS, JS, icons, manifest)
├── appsettings.json                (default config)
├── appsettings.Production.json     (production overrides — Auth on, SQLite)
├── data\                           (empty — created at first run)
├── logs\                           (empty — Serilog writes here)
├── LICENSE.txt
├── VERSION.txt                     (build timestamp + commit SHA)
├── install-service.ps1             (manual reinstall if needed)
├── uninstall-service.ps1
└── smoke-test.ps1                  (post-install validation)
```

---

## Vendor reference: minting license keys

The signing secret lives in `appsettings.Production.json` under `License:SigningSecret`. Use the same secret for both the **build that ships to customers** AND the **process you run to mint keys**.

A throwaway minting console:

```csharp
var http = new HttpClient();
var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new[] { new KeyValuePair<string,string?>("License:SigningSecret", "<your-secret>") })
    .Build();
var factory = /* your DbContext factory */;
var log = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<LicenseService>();
var svc = new LicenseService(factory, config, log);
var key = svc.MintKey("Bangura Trading", DateTime.UtcNow.AddYears(1), LicenseTier.Business, "core,sms,whatsapp,momo");
Console.WriteLine(key);
```

Output: `SALONE-QmFuZ3VyYS5UcmFkaW5nfDE3OTQ1NjAwMDB8QnVzaW5lc3N8Y29yZSxzbXMsd2hhdHNhcHAsbW9tbw.<sig>`

Email that to the customer.
