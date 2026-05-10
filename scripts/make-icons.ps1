# Generate PWA icons (192x192 and 512x512) using the SL flag mark on a navy background.
# Run once from the project root.
Add-Type -AssemblyName System.Drawing

function New-Icon {
    param([int]$Size, [string]$OutFile)
    $bmp = New-Object System.Drawing.Bitmap $Size, $Size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'

    # Background — navy gradient
    $bgRect = New-Object System.Drawing.Rectangle 0, 0, $Size, $Size
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $bgRect, ([System.Drawing.Color]::FromArgb(11, 18, 32)), ([System.Drawing.Color]::FromArgb(30, 27, 75)), 135.0
    $g.FillRectangle($bgBrush, $bgRect)

    # Centered SL flag mark, ~55% size
    $markSize = [int]($Size * 0.55)
    $markX = ($Size - $markSize) / 2
    $markY = ($Size - $markSize) / 2
    $stripe = [int]($markSize / 3)

    $green = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(30, 181, 58))
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $blue  = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0, 114, 198))
    $g.FillRectangle($green, $markX, $markY, $markSize, $stripe)
    $g.FillRectangle($white, $markX, $markY + $stripe, $markSize, $stripe)
    $g.FillRectangle($blue,  $markX, $markY + 2 * $stripe, $markSize, $stripe)

    # Border accent
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(40, 255, 255, 255)), 2
    $g.DrawRectangle($pen, $markX, $markY, $markSize, $markSize)

    $bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()
    Write-Host "Wrote $OutFile"
}

$dir = Join-Path $PSScriptRoot "..\wwwroot\icons"
New-Item -ItemType Directory -Path $dir -Force | Out-Null
New-Icon -Size 192 -OutFile (Join-Path $dir "icon-192.png")
New-Icon -Size 512 -OutFile (Join-Path $dir "icon-512.png")
Write-Host "Done." -ForegroundColor Green
