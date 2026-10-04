Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\win 10\.gemini\antigravity-ide\brain\1798eacf-9dc6-41e4-bfdd-80c93508f555\.user_uploaded\media_1790769834705.jpg"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)

# Yellow box bounds:
# X: 0 to 343 (width 344)
# Y: 22 to 232 (height 211)

# "FACESTONE" text bounds:
# X: 389 to 907 (width 519)
# Y: 59 to 129 (height 71)

# Target dimension:
# Width = 910
# Height = 211
# "FACESTONE" text is centered vertically relative to the yellow box:
# Yellow box height = 211. Text height = 71.
# Vertical margin = (211 - 71) / 2 = 70.
# In source: text starts at Y=59. Relative to yellow box top (Y=22), it was at 37.
# Shift down by (70 - 37) = 33 pixels.

$outW = 910
$outH = 211
$outLogo = New-Object System.Drawing.Bitmap($outW, $outH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

# 1. Copy Yellow CFS box (100% intact, colors preserved exactly)
for ($y = 0; $y -lt $outH; $y++) {
    $srcY = $y + 22
    for ($x = 0; $x -le 343; $x++) {
        $c = $bmp.GetPixel($x, $srcY)
        $outLogo.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
    }
}

# 2. Copy "FACESTONE" text with transparent background (ONLY white removed)
$yShift = 33

for ($srcY = 59; $srcY -le 129; $srcY++) {
    $tgtY = ($srcY - 22) + $yShift
    for ($srcX = 389; $srcX -le 907; $srcX++) {
        $c = $bmp.GetPixel($srcX, $srcY)
        $lum = [int](0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B)

        if ($lum -ge 248) {
            # White background: fully transparent
            # No pixel drawn (already transparent ARGB 0,0,0,0)
        } else {
            # Black text anti-aliasing against white background:
            # Alpha is calculated from luminance to ensure crisp, clean edges without any white fringing.
            $alpha = [int]([Math]::Round(255.0 * (1.0 - ($lum / 248.0))))
            if ($alpha -gt 255) { $alpha = 255 }
            if ($alpha -ge 12) {
                # Preserve exact black color with smooth anti-aliased alpha
                $outLogo.SetPixel($srcX, $tgtY, [System.Drawing.Color]::FromArgb($alpha, 0, 0, 0))
            }
        }
    }
}

# Ensure destination folders exist
$imgDir = "f:\FaceStoneWebsite\wwwroot\images"
if (-not (Test-Path $imgDir)) { New-Item -ItemType Directory -Path $imgDir | Out-Null }

$destPath1 = "f:\FaceStoneWebsite\wwwroot\images\facestone-logo-transparent.png"
$destPath2 = "f:\FaceStoneWebsite\wwwroot\assets\facestone-logo-transparent.png"
$destPath3 = "f:\FaceStoneWebsite\wwwroot\assets\logo.png"

$outLogo.Save($destPath1, [System.Drawing.Imaging.ImageFormat]::Png)
$outLogo.Save($destPath2, [System.Drawing.Imaging.ImageFormat]::Png)
$outLogo.Save($destPath3, [System.Drawing.Imaging.ImageFormat]::Png)

Write-Host "Successfully generated:"
Write-Host " - $destPath1"
Write-Host " - $destPath2"
Write-Host " - $destPath3"

$outLogo.Dispose()
$bmp.Dispose()
