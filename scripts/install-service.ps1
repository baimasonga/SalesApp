<#
.SYNOPSIS
    Installs Salone Sales as a Windows Service that auto-starts on boot.
    Must be run as Administrator from the published folder (the one that contains SalesApp.exe).

.PARAMETER ServiceName
    Service name. Defaults to "SaloneSales".

.PARAMETER DisplayName
    Display name shown in services.msc. Defaults to "Salone Sales".

.PARAMETER Port
    Listening port. Defaults to 5099.

.PARAMETER Description
    Service description.

.EXAMPLE
    # From an elevated PowerShell, in the publish folder:
    powershell -ExecutionPolicy Bypass -File install-service.ps1
    powershell -ExecutionPolicy Bypass -File install-service.ps1 -Port 8080
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "SaloneSales",
    [string]$DisplayName = "Salone Sales",
    [int]$Port = 5099,
    [string]$Description = "Salone Sales — sales, inventory and CRM for Sierra Leone businesses."
)

$ErrorActionPreference = "Stop"
$exe = Join-Path $PSScriptRoot "SalesApp.exe"
if (-not (Test-Path $exe)) {
    Write-Host "Cannot find SalesApp.exe in $PSScriptRoot." -ForegroundColor Red
    Write-Host "Run this script from the publish folder produced by scripts\publish-win.ps1." -ForegroundColor Red
    exit 1
}

# Require admin
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Must be run as Administrator. Right-click PowerShell → Run as Administrator." -ForegroundColor Red
    exit 1
}

# Stop+remove any existing service so we re-install cleanly
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Existing service '$ServiceName' found. Stopping and removing first..." -ForegroundColor Yellow
    Stop-Service $ServiceName -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Service binary path with quoted exe + URLs argument
$binPath = "`"$exe`" --urls=http://+:$Port"
Write-Host "Registering service ..." -ForegroundColor Cyan
$result = sc.exe create $ServiceName binPath= $binPath start= auto DisplayName= $DisplayName
if ($LASTEXITCODE -ne 0) { Write-Host $result -ForegroundColor Red; exit 1 }

sc.exe description $ServiceName $Description | Out-Null

# Tell the app to integrate with the SCM (Program.cs reads RUN_AS_SERVICE=1)
[Environment]::SetEnvironmentVariable("RUN_AS_SERVICE", "1", "Machine")

# Recovery: restart on failure
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

# Firewall (LAN only, not internet)
$ruleName = "Salone Sales (Port $Port)"
Remove-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue | Out-Null
New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow `
    -Protocol TCP -LocalPort $Port -Profile Private,Domain | Out-Null
Write-Host "Firewall: allowed TCP $Port on Private+Domain profiles" -ForegroundColor Green

# Start the service
Start-Service $ServiceName
Start-Sleep -Seconds 3

# Health probe
try {
    $r = Invoke-WebRequest -Uri "http://localhost:$Port/health/live" -TimeoutSec 8 -SkipHttpErrorCheck
    if ($r.StatusCode -eq 200) {
        Write-Host ""
        Write-Host "Service is running." -ForegroundColor Green
        Write-Host "  URL:        http://localhost:$Port"
        Write-Host "  Status:     $((Get-Service $ServiceName).Status)"
        Write-Host "  Open in browser, sign in as Admin, go to /settings to configure."
        Write-Host ""
    } else {
        Write-Host "Service installed but /health/live returned $($r.StatusCode). Check the Event Log / logs folder." -ForegroundColor Yellow
    }
} catch {
    Write-Host "Service installed but did not respond on http://localhost:$Port. Check logs/" -ForegroundColor Yellow
}
