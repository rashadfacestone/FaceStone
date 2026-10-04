Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\win 10\.gemini\antigravity-ide\brain\1798eacf-9dc6-41e4-bfdd-80c93508f555\.user_uploaded\media_1790769834705.jpg"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)

$minX = 344; $maxX = 0
$minY = 232; $maxY = 0

for ($y = 22; $y -le 232; $y++) {
    for ($x = 0; $x -le 343; $x++) {
        $c = $bmp.GetPixel($x, $y)
        # Check if black/dark
        if ($c.R -lt 100 -and $c.G -lt 100 -and $c.B -lt 100) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}

Write-Host "CFS emblem black pixels inside yellow box: X=[$minX, $maxX], Y=[$minY, $maxY], W=$($maxX - $minX + 1), H=$($maxY - $minY + 1)"
$bmp.Dispose()
