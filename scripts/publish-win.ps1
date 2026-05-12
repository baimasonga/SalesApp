<#
.SYNOPSIS
    Publish Salone Sales as a self-contained Windows x64 deployment.
    The output folder includes the .NET runtime so target machines DON'T need
    .NET installed — only Windows 10 / Server 2016 or newer.

.PARAMETER OutDir
    Where to drop the published folder. Defaults to ./publish/win-x64.

.PARAMETER Runtime
    Target runtime ID. Defaults to win-x64. Use win-x86 for 32-bit, win-arm64 for ARM.

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/publish-win.ps1
    powershell -ExecutionPolicy Bypass -File scripts/publish-win.ps1 -OutDir D:\Deploy\SaloneSales
#>
[CmdletBinding()]
param(
    [string]$OutDir = "$PSScriptRoot/../publish/win-x64",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot/.."
Push-Location $root

try {
    Write-Host ""
    Write-Host "Publishing Salone Sales → $OutDir" -ForegroundColor Cyan
    Write-Host "  Runtime: $Runtime  ·  Config: $Configuration" -ForegroundColor Gray
    Write-Host ""

    # Capture commit SHA for /api/version (best-effort)
    $commit = "unknown"
    try {
        $g = git rev-parse --short HEAD 2>$null
        if ($LASTEXITCODE -eq 0) { $commit = $g }
    } catch { }

    if (Test-Path $OutDir) {
        Write-Host "  Cleaning $OutDir ..." -ForegroundColor Yellow
        Remove-Item $OutDir -Recurse -Force
    }

    dotnet publish `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:PublishTrimmed=false `
        -p:DebugType=embedded `
        -o $OutDir

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

    # Drop in supporting assets the installer needs
    Copy-Item "$root/scripts/install-service.ps1"   "$OutDir/install-service.ps1"
    Copy-Item "$root/scripts/uninstall-service.ps1" "$OutDir/uninstall-service.ps1"
    Copy-Item "$root/scripts/smoke-test.ps1"        "$OutDir/smoke-test.ps1"
    Copy-Item "$root/DESKTOP-INSTALL.md"            "$OutDir/README.txt" -ErrorAction SilentlyContinue
    Copy-Item "$root/LICENSE.txt"                   "$OutDir/LICENSE.txt" -ErrorAction SilentlyContinue

    # Ensure runtime config slots for SQLite + ServiceName by overlaying appsettings.Production
    if (Test-Path "$root/appsettings.Production.json") {
        Copy-Item "$root/appsettings.Production.json" "$OutDir/appsettings.Production.json" -Force
    }

    # Empty data + logs dirs (service writes to them)
    New-Item -ItemType Directory -Force -Path "$OutDir/data" | Out-Null
    New-Item -ItemType Directory -Force -Path "$OutDir/logs" | Out-Null

    # Stamp version info file
    $version = (Get-Item "$OutDir/SalesApp.dll").VersionInfo.FileVersion
    @"
Salone Sales — Sierra Leone Business OS
Built: $(Get-Date -Format "u")
Version: $version
Commit: $commit
Runtime: $Runtime
Configuration: $Configuration
"@ | Out-File "$OutDir/VERSION.txt" -Encoding utf8

    $size = "{0:N1} MB" -f ((Get-ChildItem $OutDir -Recurse | Measure-Object Length -Sum).Sum / 1MB)

    Write-Host ""
    Write-Host "Published: $OutDir" -ForegroundColor Green
    Write-Host "  Size:    $size"
    Write-Host "  Version: $version"
    Write-Host "  Commit:  $commit"
    Write-Host ""
    Write-Host "Next:"
    Write-Host "  cd $OutDir"
    Write-Host "  powershell -ExecutionPolicy Bypass -File install-service.ps1"
    Write-Host ""
}
finally {
    Pop-Location
}
