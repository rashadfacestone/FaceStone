Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\win 10\.gemini\antigravity-ide\brain\1798eacf-9dc6-41e4-bfdd-80c93508f555\.user_uploaded\media_1790769834705.jpg"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)

$minX = $bmp.Width; $maxX = 0
$minY = $bmp.Height; $maxY = 0

for ($y = 0; $y -lt $bmp.Height; $y++) {
    for ($x = 0; $x -lt $bmp.Width; $x++) {
        $c = $bmp.GetPixel($x, $y)
        # Check if NOT white (or near white)
        if ($c.R -lt 245 -or $c.G -lt 245 -or $c.B -lt 245) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}

Write-Host "Non-white bounding box: X=[$minX, $maxX], Y=[$minY, $maxY], W=$($maxX - $minX + 1), H=$($maxY - $minY + 1)"

# Check the yellow box specifically (where R > 180, G > 140, B < 100)
$yellowMinX = $bmp.Width; $yellowMaxX = 0
$yellowMinY = $bmp.Height; $yellowMaxY = 0

for ($y = 0; $y -lt $bmp.Height; $y++) {
    for ($x = 0; $x -lt $bmp.Width; $x++) {
        $c = $bmp.GetPixel($x, $y)
        if ($c.R -gt 180 -and $c.G -gt 130 -and $c.B -lt 120) {
            if ($x -lt $yellowMinX) { $yellowMinX = $x }
            if ($x -gt $yellowMaxX) { $yellowMaxX = $x }
            if ($y -lt $yellowMinY) { $yellowMinY = $y }
            if ($y -gt $yellowMaxY) { $yellowMaxY = $y }
        }
    }
}
Write-Host "Yellow box bounding box: X=[$yellowMinX, $yellowMaxX], Y=[$yellowMinY, $yellowMaxY], W=$($yellowMaxX - $yellowMinX + 1), H=$($yellowMaxY - $yellowMinY + 1)"

# Check the text regions (X > yellowMaxX and dark pixels)
$textMinX = $bmp.Width; $textMaxX = 0
$textMinY = $bmp.Height; $textMaxY = 0

for ($y = 0; $y -lt $bmp.Height; $y++) {
    for ($x = ($yellowMaxX + 1); $x -lt $bmp.Width; $x++) {
        $c = $bmp.GetPixel($x, $y)
        if ($c.R -lt 150 -and $c.G -lt 150 -and $c.B -lt 150) {
            if ($x -lt $textMinX) { $textMinX = $x }
            if ($x -gt $textMaxX) { $textMaxX = $x }
            if ($y -lt $textMinY) { $textMinY = $y }
            if ($y -gt $textMaxY) { $textMaxY = $y }
        }
    }
}
Write-Host "Text bounding box: X=[$textMinX, $textMaxX], Y=[$textMinY, $textMaxY], W=$($textMaxX - $textMinX + 1), H=$($textMaxY - $textMinY + 1)"

$bmp.Dispose()
