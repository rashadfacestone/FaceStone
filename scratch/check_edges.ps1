Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\win 10\.gemini\antigravity-ide\brain\1798eacf-9dc6-41e4-bfdd-80c93508f555\.user_uploaded\media_1790769834705.jpg"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)

Write-Host "Left edge x=0 to 10:"
for ($x = 0; $x -le 10; $x++) {
    $c = $bmp.GetPixel($x, 100)
    Write-Host ("x={0}, y=100: R={1}, G={2}, B={3}" -f $x, $c.R, $c.G, $c.B)
}

Write-Host "Yellow box right edge around y=100:"
for ($x = 340; $x -le 355; $x++) {
    $c = $bmp.GetPixel($x, 100)
    Write-Host ("x={0}, y=100: R={1}, G={2}, B={3}" -f $x, $c.R, $c.G, $c.B)
}

Write-Host "Yellow box top edge at x=100:"
for ($y = 15; $y -le 30; $y++) {
    $c = $bmp.GetPixel(100, $y)
    Write-Host ("x=100, y={0}: R={1}, G={2}, B={3}" -f $y, $c.R, $c.G, $c.B)
}

Write-Host "Yellow box bottom edge at x=100:"
for ($y = 225; $y -le 240; $y++) {
    $c = $bmp.GetPixel(100, $y)
    Write-Host ("x=100, y={0}: R={1}, G={2}, B={3}" -f $y, $c.R, $c.G, $c.B)
}

$bmp.Dispose()
