Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\win 10\.gemini\antigravity-ide\brain\1798eacf-9dc6-41e4-bfdd-80c93508f555\.user_uploaded\media_1790769834705.jpg"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)

# 1. Full transparent logo.png
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
            # Inside the yellow box: keep completely solid
            $outLogo.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
        } else {
            # Text area with white background to be made transparent
            $lum = [int](0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B)
            if ($lum -ge 248) {
                $outLogo.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
            } else {
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

$logoDest = "f:\FaceStoneWebsite\wwwroot\assets\logo.png"
$outLogo.Save($logoDest, [System.Drawing.Imaging.ImageFormat]::Png)
$outLogo.Dispose()
Write-Host "Updated $logoDest"

# 2. Square Brand Icons (192, 512, maskable, apple-touch)
$yellowColor = $bmp.GetPixel(100, 100) # R=238, G=189, B=50

function Make-SquareIcon([int]$size, [string]$outPath, [bool]$maskable = $false) {
    $outBmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($outBmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    if ($maskable) {
        $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#0B111E'))
        $g.FillRectangle($bgBrush, 0, 0, $size, $size)
        $bgBrush.Dispose()

        $innerSize = [int]($size * 0.70)
        $innerOffset = [int](($size - $innerSize) / 2)

        $tileBrush = New-Object System.Drawing.SolidBrush($yellowColor)
        $rect = New-Object System.Drawing.Rectangle($innerOffset, $innerOffset, $innerSize, $innerSize)
        $g.FillRectangle($tileBrush, $rect)
        $tileBrush.Dispose()

        $pad = [int]($innerSize * 0.08)
        $destX = [int]($innerOffset + $pad)
        $destY = [int]($innerOffset + $pad)
        $destW = [int]($innerSize - (2 * $pad))
        $destH = [int]($innerSize - (2 * $pad))

        $cfsSrcRect = New-Object System.Drawing.Rectangle(20, 35, 305, 195)
        $destRect = New-Object System.Drawing.Rectangle($destX, $destY, $destW, $destH)
        $g.DrawImage($bmp, $destRect, $cfsSrcRect, [System.Drawing.GraphicsUnit]::Pixel)
    } else {
        $bgBrush = New-Object System.Drawing.SolidBrush($yellowColor)
        $g.FillRectangle($bgBrush, 0, 0, $size, $size)
        $bgBrush.Dispose()

        $pad = [int]($size * 0.08)
        $destX = $pad
        $destY = $pad
        $destW = [int]($size - (2 * $pad))
        $destH = [int]($size - (2 * $pad))

        $cfsSrcRect = New-Object System.Drawing.Rectangle(20, 35, 305, 195)
        $destRect = New-Object System.Drawing.Rectangle($destX, $destY, $destW, $destH)
        $g.DrawImage($bmp, $destRect, $cfsSrcRect, [System.Drawing.GraphicsUnit]::Pixel)
    }

    $g.Dispose()
    $outBmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $outBmp.Dispose()
    Write-Host "Created $outPath"
}

Make-SquareIcon 192 "f:\FaceStoneWebsite\wwwroot\assets\icon-192.png" $false
Make-SquareIcon 512 "f:\FaceStoneWebsite\wwwroot\assets\icon-512.png" $false
Make-SquareIcon 512 "f:\FaceStoneWebsite\wwwroot\assets\icon-maskable-512.png" $true
Make-SquareIcon 180 "f:\FaceStoneWebsite\wwwroot\assets\apple-touch-icon.png" $false

# 3. Create favicon.ico
$favBmp = New-Object System.Drawing.Bitmap(32, 32, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$favG = [System.Drawing.Graphics]::FromImage($favBmp)
$favG.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$favG.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$favG.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

$favBrush = New-Object System.Drawing.SolidBrush($yellowColor)
$favG.FillRectangle($favBrush, 0, 0, 32, 32)
$favBrush.Dispose()

$cfsSrcRect = New-Object System.Drawing.Rectangle(20, 35, 305, 195)
$favDestRect = New-Object System.Drawing.Rectangle(2, 2, 28, 28)
$favG.DrawImage($bmp, $favDestRect, $cfsSrcRect, [System.Drawing.GraphicsUnit]::Pixel)
$favG.Dispose()

$favIconPath = "f:\FaceStoneWebsite\wwwroot\favicon.ico"
$iconPtr = $favBmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($iconPtr)
$fs = [System.IO.File]::OpenWrite($favIconPath)
$icon.Save($fs)
$fs.Close()
$icon.Dispose()
$favBmp.Dispose()
Write-Host "Created $favIconPath"

$bmp.Dispose()
