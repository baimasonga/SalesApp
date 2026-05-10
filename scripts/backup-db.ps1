# Salone Sales — daily SQL Express backup
# Schedule via Windows Task Scheduler to run every day at e.g. 02:00.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts\backup-db.ps1 -BackupDir "C:\SalesAppBackups"

param(
    [string]$Server = ".\SQLEXPRESS",
    [string]$Database = "SalesAppDb",
    [string]$BackupDir = "C:\SalesAppBackups",
    [int]$KeepDays = 30
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$file = Join-Path $BackupDir "$Database`_$timestamp.bak"

Write-Host "==> Backing up $Database to $file" -ForegroundColor Cyan
$query = "BACKUP DATABASE [$Database] TO DISK = N'$file' WITH FORMAT, INIT, COMPRESSION, NAME = N'$Database-Full Database Backup'"
sqlcmd -S $Server -E -Q $query

Write-Host "==> Pruning backups older than $KeepDays days..." -ForegroundColor Cyan
Get-ChildItem $BackupDir -Filter "$Database`_*.bak" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays) } |
    Remove-Item -Force

Write-Host "==> Backup complete." -ForegroundColor Green
