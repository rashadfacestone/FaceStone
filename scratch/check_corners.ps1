Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\win 10\.gemini\antigravity-ide\brain\1798eacf-9dc6-41e4-bfdd-80c93508f555\.user_uploaded\media_1790769834705.jpg"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)

Write-Host "Corner top-left: x=0, y=22: $($bmp.GetPixel(0, 22))"
Write-Host "Inside top-left: x=5, y=27: $($bmp.GetPixel(5, 27))"
Write-Host "Corner top-right: x=343, y=22: $($bmp.GetPixel(343, 22))"
Write-Host "Corner bottom-left: x=0, y=232: $($bmp.GetPixel(0, 232))"
Write-Host "Corner bottom-right: x=343, y=232: $($bmp.GetPixel(343, 232))"

$bmp.Dispose()
