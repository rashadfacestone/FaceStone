Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\win 10\.gemini\antigravity-ide\brain\1798eacf-9dc6-41e4-bfdd-80c93508f555\.user_uploaded\media_1790769834705.jpg"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)

$cropY0 = 22
$cropY1 = 232
$cropH = $cropY1 - $cropY0 + 1  # 211
$cropW = 1014                  # 0 to 1013

$outLogo = New-Object System.Drawing.Bitmap($cropW, $cropH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

for ($y = 0; $y -lt $cropH; $y++) {
    $srcY = $y + $cropY0
    for ($x = 0; $x -lt $cropW; $x++) {
        $srcX = $x
        $c = $bmp.GetPixel($srcX, $srcY)

        if ($srcX -le 343) {
            # Inside the yellow box
            $outLogo.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
        } else {
            # Text area with white background to be made transparent
            $lum = [int](0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B)
            
            # Pure white or near-white background
            if ($lum -ge 248) {
                $outLogo.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
            } else {
                # Smooth alpha falloff from black (lum=0 -> alpha=255) to background (lum=248 -> alpha=0)
                $alpha = [int]([Math]::Round(255.0 * (1.0 - ($lum / 248.0))))
                if ($alpha -gt 255) { $alpha = 255 }
                if ($alpha -lt 12) {
                    $outLogo.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
                } else {
                    $outLogo.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, 0, 0, 0))
                }
            }
        }
    }
}

$destPath = "f:\FaceStoneWebsite\wwwroot\assets\logo.png"
$outLogo.Save($destPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Successfully saved transparent logo to $destPath ($cropW x $cropH)"

$outLogo.Dispose()
$bmp.Dispose()
