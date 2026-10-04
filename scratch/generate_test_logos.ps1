Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\win 10\.gemini\antigravity-ide\brain\1798eacf-9dc6-41e4-bfdd-80c93508f555\.user_uploaded\media_1790769834705.jpg"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)

# 1. Full logo with transparent background
# Bounding box of all content:
# Yellow box: x: 0..343, y: 22..232
# Text: x: 389..1012, y: 59..193
# Overall content Y: 22 to 232 (height: 211)
# Overall content X: 0 to 1013 (width: 1014)

$cropY0 = 22
$cropY1 = 232
$cropH = $cropY1 - $cropY0 + 1
$cropW = 1014

$outFull = New-Object System.Drawing.Bitmap($cropW, $cropH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

for ($y = 0; $y -lt $cropH; $y++) {
    $srcY = $y + $cropY0
    for ($x = 0; $x -lt $cropW; $x++) {
        $srcX = $x
        $c = $bmp.GetPixel($srcX, $srcY)

        if ($srcX -le 343) {
            # Inside the yellow box area
            # At srcY between 22 and 232, the yellow box is completely solid!
            $outFull.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
        } else {
            # Text area with white background
            # Compute alpha from luminance
            # If pixel is white (R,G,B near 255), alpha = 0
            # If pixel is dark, alpha = 255 - luminance, color is black
            $lum = [int](0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B)
            if ($lum -ge 250) {
                $outFull.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
            } else {
                # Anti-aliased black text
                $alpha = 255 - $lum
                # Ensure smooth falloff
                if ($alpha -lt 15) { $alpha = 0 }
                $outFull.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, 0, 0, 0))
            }
        }
    }
}

$outFull.Save("f:\FaceStoneWebsite\scratch\logo_full_transparent.png", [System.Drawing.Imaging.ImageFormat]::Png)
$outFull.Dispose()
Write-Host "Generated logo_full_transparent.png"

# 2. "FACESTONE ONLY" logo
# Here we only include:
# - Yellow box: 344 x 211
# - Gap of ~ 45px
# - "FACESTONE" text (y=59..129), centered vertically relative to the yellow box!
# The yellow box height is 211.
# "FACESTONE" height is 71 (from 59 to 129).
# To center "FACESTONE" vertically: (211 - 71) / 2 = 70.
# In the original image: y=59 is at offset (59 - 22) = 37. So it was in the upper half!
# In "FACESTONE ONLY", centering it vertically looks balanced and professional.
# Width of "FACESTONE" text: from x=389 to x=887 (width ~ 500)
$textFaceStoneMaxX = 0
for ($y = 59; $y -le 129; $y++) {
    for ($x = 389; $x -lt $bmp.Width; $x++) {
        $c = $bmp.GetPixel($x, $y)
        if ($c.R -lt 150) {
            if ($x -gt $textFaceStoneMaxX) { $textFaceStoneMaxX = $x }
        }
    }
}
Write-Host "FACESTONE text right edge: $textFaceStoneMaxX"

$bmp.Dispose()
