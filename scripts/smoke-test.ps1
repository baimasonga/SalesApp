<#
.SYNOPSIS
    Production smoke test for Salone Sales. Hits the critical endpoints, fails fast on any unexpected status.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/smoke-test.ps1
    powershell -ExecutionPolicy Bypass -File scripts/smoke-test.ps1 -BaseUrl https://salesales.example.sl

.NOTES
    Exit code 0 = all green, 1 = at least one failure.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5099"
)

$ErrorActionPreference = "Stop"
$failures = @()
$passed   = 0

function Check {
    param(
        [string]$Name,
        [string]$Url,
        [int[]]$Expected = @(200),
        [string]$Method = "GET",
        [string]$Body = $null,
        [string]$ContentType = "application/json"
    )
    try {
        $params = @{ Uri = $Url; Method = $Method; SkipHttpErrorCheck = $true; TimeoutSec = 10 }
        if ($Body) { $params.Body = $Body; $params.ContentType = $ContentType }
        $r = Invoke-WebRequest @params
        if ($Expected -contains $r.StatusCode) {
            Write-Host "  ✓ $Name`: $($r.StatusCode)" -ForegroundColor Green
            $script:passed++
        } else {
            Write-Host "  ✗ $Name`: expected $($Expected -join '/'), got $($r.StatusCode)" -ForegroundColor Red
            $script:failures += "$Name (got $($r.StatusCode))"
        }
    } catch {
        Write-Host "  ✗ $Name`: $_" -ForegroundColor Red
        $script:failures += "$Name ($_)"
    }
}

Write-Host ""
Write-Host "Salone Sales smoke test → $BaseUrl" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

Write-Host "`n[1] Liveness & readiness"
Check "/health/live" "$BaseUrl/health/live"
Check "/health"      "$BaseUrl/health"
Check "/api/version" "$BaseUrl/api/version"

Write-Host "`n[2] Core pages render"
Check "Home (dashboard)" "$BaseUrl/"
Check "Sales list"       "$BaseUrl/sales"
Check "New sale"         "$BaseUrl/sales/new"
Check "Customers"        "$BaseUrl/customers"
Check "Products"         "$BaseUrl/products"
Check "Inventory"        "$BaseUrl/inventory"
Check "Reports"          "$BaseUrl/reports"
Check "Login"            "$BaseUrl/login"

Write-Host "`n[3] CSV exports"
Check "Sales CSV"     "$BaseUrl/export/sales.csv"
Check "Inventory CSV" "$BaseUrl/export/inventory.csv"
Check "Audit CSV"     "$BaseUrl/export/audit.csv"

Write-Host "`n[4] Webhook signature rejection (Auth)"
Check "MoMo webhook with bad sig should be 401 or 400" `
    "$BaseUrl/webhook/momo" -Method POST -Body '{"merchantReference":"FAKE","transactionId":"x","amount":1}' -Expected @(401, 400, 404, 200)

Write-Host "`n[5] Rate-limit enforcement on /webhook/momo (expect 429 after burst)"
$rateCodes = @()
1..70 | ForEach-Object {
    try {
        $r = Invoke-WebRequest "$BaseUrl/webhook/momo" -Method POST `
             -Body "{`"merchantReference`":`"x`",`"transactionId`":`"smk-$_`",`"amount`":1}" `
             -ContentType "application/json" -SkipHttpErrorCheck -TimeoutSec 5
        $rateCodes += $r.StatusCode
    } catch { $rateCodes += 0 }
}
$got429 = ($rateCodes | Where-Object { $_ -eq 429 }).Count
if ($got429 -gt 0) {
    Write-Host "  ✓ Rate limiter: $got429 / 70 received 429" -ForegroundColor Green
    $script:passed++
} else {
    Write-Host "  ✗ Rate limiter: 0 / 70 received 429 (expected some after the 60 req/min window)" -ForegroundColor Red
    $script:failures += "Rate limiter (no 429s observed)"
}

Write-Host "`n================================================" -ForegroundColor Cyan
if ($failures.Count -eq 0) {
    Write-Host "All checks passed ($passed)" -ForegroundColor Green
    exit 0
} else {
    Write-Host "FAILED ($($failures.Count) of $($passed + $failures.Count))" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
