Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\win 10\.gemini\antigravity-ide\brain\1798eacf-9dc6-41e4-bfdd-80c93508f555\.user_uploaded\media_1790769834705.jpg"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)

# Project horizontal projection of dark pixels in text region
for ($y = 50; $y -le 200; $y++) {
    $darkCount = 0
    for ($x = 380; $x -lt $bmp.Width; $x++) {
        $c = $bmp.GetPixel($x, $y)
        if ($c.R -lt 150) { $darkCount++ }
    }
    if ($darkCount -gt 0) {
        Write-Host "Y=$y : darkCount=$darkCount"
    }
}
$bmp.Dispose()
