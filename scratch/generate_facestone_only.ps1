Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\win 10\.gemini\antigravity-ide\brain\1798eacf-9dc6-41e4-bfdd-80c93508f555\.user_uploaded\media_1790769834705.jpg"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)

# Yellow box bounds:
# X: 0 to 343 (width: 344)
# Y: 23 to 232 (height: 210)

# "FACESTONE" text bounds:
# X: 389 to 907 (width: 519)
# Y: 59 to 129 (height: 71)

# In the original, distance from yellow box (x=343) to text (x=389) is 46px.
# Height of yellow box is 210px.
# If we center "FACESTONE" text vertically against 210px:
# vertical offset = (210 - 71) / 2 = 69.5 -> ~70px from top of yellow box.
# In original, (59 - 23) = 36px from top.
# Total width = 344 + 46 + 519 = 909px.
# Total height = 210px.

$outOnly = New-Object System.Drawing.Bitmap(910, 210, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

# 1. Copy yellow box
for ($y = 0; $y -lt 210; $y++) {
    $srcY = $y + 23
    for ($x = 0; $x -le 343; $x++) {
        $c = $bmp.GetPixel($x, $srcY)
        $outOnly.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
    }
}

# 2. Copy "FACESTONE" text, centered vertically:
# Text original: Y from 59 to 129
# Target Y: starts at 70
$yOffset = 70 - (59 - 23) # = 70 - 36 = 34px shift downwards

for ($srcY = 59; $srcY -le 129; $srcY++) {
    $tgtY = $srcY - 23 + $yOffset
    for ($srcX = 389; $srcX -le 907; $srcX++) {
        $c = $bmp.GetPixel($srcX, $srcY)
        $lum = [int](0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B)
        if ($lum -ge 248) {
            # transparent
        } else {
            $alpha = 255 - $lum
            if ($alpha -gt 15) {
                # In target image, x is same as srcX
                $outOnly.SetPixel($srcX, $tgtY, [System.Drawing.Color]::FromArgb($alpha, 0, 0, 0))
            }
        }
    }
}

$outOnly.Save("f:\FaceStoneWebsite\scratch\logo_facestone_only_transparent.png", [System.Drawing.Imaging.ImageFormat]::Png)
$outOnly.Dispose()
$bmp.Dispose()
Write-Host "Generated logo_facestone_only_transparent.png"
