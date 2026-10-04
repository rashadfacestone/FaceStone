Add-Type -AssemblyName System.Drawing

$logoPath = "f:\FaceStoneWebsite\wwwroot\images\facestone-logo-transparent.png"
$bmp = [System.Drawing.Bitmap]::FromFile($logoPath)

Write-Host "Logo dimensions: $($bmp.Width) x $($bmp.Height)"

# Check transparency of top-right background (x=800, y=20)
$bgPixel = $bmp.GetPixel(800, 20)
Write-Host "Background pixel at (800, 20): Alpha=$($bgPixel.A), R=$($bgPixel.R), G=$($bgPixel.G), B=$($bgPixel.B)"

# Check yellow box pixel (x=100, y=100)
$yellowPixel = $bmp.GetPixel(100, 100)
Write-Host "Yellow box pixel at (100, 100): Alpha=$($yellowPixel.A), R=$($yellowPixel.R), G=$($yellowPixel.G), B=$($yellowPixel.B)"

# Check black text pixel in FACESTONE (x=500, y=105)
# Find a black pixel in the 'E' or 'S' of FACESTONE
$foundBlack = $false
for ($y = 80; $y -le 125; $y++) {
    for ($x = 450; $x -le 600; $x++) {
        $c = $bmp.GetPixel($x, $y)
        if ($c.A -eq 255 -and $c.R -lt 10 -and $c.G -lt 10 -and $c.B -lt 10) {
            Write-Host "Black text pixel at ($x, $y): Alpha=$($c.A), R=$($c.R), G=$($c.G), B=$($c.B)"
            $foundBlack = $true
            break
        }
    }
    if ($foundBlack) { break }
}

# Check bottom area where "Full Concrete Solutions" used to be:
# In the original, subtext was y=155..193.
# In the target 211px image, below y=140 in the text area (x > 343), all pixels must be transparent (Alpha = 0)!
$subtextPixels = 0
for ($y = 145; $y -lt $bmp.Height; $y++) {
    for ($x = 350; $x -lt $bmp.Width; $x++) {
        $c = $bmp.GetPixel($x, $y)
        if ($c.A -gt 0) {
            $subtextPixels++
        }
    }
}
Write-Host "Non-transparent pixels below y=145 in text area: $subtextPixels (MUST BE 0)"

$bmp.Dispose()
