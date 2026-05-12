<#
.SYNOPSIS
    Removes the Salone Sales Windows Service and its firewall rule.
    The data folder (data\salone.db) is intentionally LEFT IN PLACE.
    To wipe data too, manually delete the data/ and logs/ folders.

.EXAMPLE
    # From an elevated PowerShell, in the publish folder:
    powershell -ExecutionPolicy Bypass -File uninstall-service.ps1
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "SaloneSales",
    [int]$Port = 5099
)

$ErrorActionPreference = "SilentlyContinue"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Must be run as Administrator." -ForegroundColor Red
    exit 1
}

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Stopping $ServiceName ..." -ForegroundColor Cyan
    Stop-Service $ServiceName -Force
    Start-Sleep 1
    Write-Host "Removing service ..." -ForegroundColor Cyan
    sc.exe delete $ServiceName | Out-Null
    Write-Host "Service removed." -ForegroundColor Green
} else {
    Write-Host "Service '$ServiceName' not installed." -ForegroundColor Yellow
}

# Firewall
Remove-NetFirewallRule -DisplayName "Salone Sales (Port $Port)" -ErrorAction SilentlyContinue
[Environment]::SetEnvironmentVariable("RUN_AS_SERVICE", $null, "Machine")

Write-Host ""
Write-Host "Data preserved in: $PSScriptRoot\data\" -ForegroundColor Cyan
Write-Host "Logs preserved in: $PSScriptRoot\logs\" -ForegroundColor Cyan
Write-Host "Delete those folders manually if you want a clean wipe."
