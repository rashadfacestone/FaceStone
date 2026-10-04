Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\win 10\.gemini\antigravity-ide\brain\1798eacf-9dc6-41e4-bfdd-80c93508f555\.user_uploaded\media_1790769834705.jpg"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)

$minWhiteR = 255; $minWhiteG = 255; $minWhiteB = 255
for ($y = 0; $y -lt 40; $y++) {
    for ($x = 400; $x -lt 800; $x++) {
        $c = $bmp.GetPixel($x, $y)
        if ($c.R -lt $minWhiteR) { $minWhiteR = $c.R }
        if ($c.G -lt $minWhiteG) { $minWhiteG = $c.G }
        if ($c.B -lt $minWhiteB) { $minWhiteB = $c.B }
    }
}
Write-Host "In background region: min R=$minWhiteR, min G=$minWhiteG, min B=$minWhiteB"
$bmp.Dispose()
