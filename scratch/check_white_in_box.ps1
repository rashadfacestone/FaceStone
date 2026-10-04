Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\win 10\.gemini\antigravity-ide\brain\1798eacf-9dc6-41e4-bfdd-80c93508f555\.user_uploaded\media_1790769834705.jpg"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)

$whiteInBox = 0
for ($y = 25; $y -le 230; $y++) {
    for ($x = 5; $x -le 338; $x++) {
        $c = $bmp.GetPixel($x, $y)
        if ($c.R -gt 240 -and $c.G -gt 240 -and $c.B -gt 240) {
            $whiteInBox++
        }
    }
}
Write-Host "White pixels inside yellow box: $whiteInBox"
$bmp.Dispose()
