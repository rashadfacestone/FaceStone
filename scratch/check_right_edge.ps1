Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\win 10\.gemini\antigravity-ide\brain\1798eacf-9dc6-41e4-bfdd-80c93508f555\.user_uploaded\media_1790769834705.jpg"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)

for ($x = 1005; $x -lt $bmp.Width; $x++) {
    $minLum = 255
    $minLumY = 0
    for ($y = 50; $y -lt 200; $y++) {
        $c = $bmp.GetPixel($x, $y)
        $lum = [int](0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B)
        if ($lum -lt $minLum) {
            $minLum = $lum
            $minLumY = $y
        }
    }
    Write-Host ("x={0}: minLum={1} at y={2}" -f $x, $minLum, $minLumY)
}
$bmp.Dispose()
