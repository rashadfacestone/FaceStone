Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\win 10\.gemini\antigravity-ide\brain\1798eacf-9dc6-41e4-bfdd-80c93508f555\.user_uploaded\media_1790769834705.jpg"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)

for ($y = 22; $y -le 232; $y += 30) {
    # Find where yellow ends
    for ($x = 335; $x -le 350; $x++) {
        $c = $bmp.GetPixel($x, $y)
        if ($c.R -gt 245 -and $c.G -gt 245 -and $c.B -gt 245) {
            Write-Host "At y=$y, first white pixel at x=$x (prev pixel R=$($bmp.GetPixel($x-1,$y).R), G=$($bmp.GetPixel($x-1,$y).G), B=$($bmp.GetPixel($x-1,$y).B))"
            break
        }
    }
}
$bmp.Dispose()
