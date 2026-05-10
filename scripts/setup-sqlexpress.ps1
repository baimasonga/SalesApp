# Salone Sales — SQL Express setup script
#
# Prerequisites:
#   1. Install SQL Server Express: https://www.microsoft.com/sql-server/sql-server-downloads
#      (pick "Express" edition; default instance name SQLEXPRESS).
#   2. Install dotnet-ef tool:  dotnet tool install --global dotnet-ef
#
# Run from project root:
#   powershell -ExecutionPolicy Bypass -File scripts\setup-sqlexpress.ps1

param(
    [string]$Server = ".\SQLEXPRESS",
    [string]$Database = "SalesAppDb"
)

$ErrorActionPreference = "Stop"

Write-Host "==> Verifying SQL Server connectivity..." -ForegroundColor Cyan
try {
    sqlcmd -S $Server -E -Q "SELECT @@VERSION" -h -1 | Select-Object -First 1
} catch {
    Write-Error "Cannot reach $Server. Make sure SQL Express is running and the SQLEXPRESS instance is enabled."
    exit 1
}

Write-Host "==> Switching appsettings.json to SqlServer provider..." -ForegroundColor Cyan
$config = Get-Content -Raw "appsettings.json" | ConvertFrom-Json
$config.Database.Provider = "SqlServer"
$config | ConvertTo-Json -Depth 10 | Set-Content "appsettings.json"

Write-Host "==> Running EF Core migrations..." -ForegroundColor Cyan
dotnet ef migrations add InitialCreate --context SalesDbContext --output-dir Data/Migrations 2>$null
dotnet ef database update --context SalesDbContext

Write-Host "==> Done! Run 'dotnet run' and navigate to https://localhost:5099" -ForegroundColor Green
Write-Host "    To switch back to in-memory mode, set Database:Provider to 'InMemory' in appsettings.json." -ForegroundColor Gray
