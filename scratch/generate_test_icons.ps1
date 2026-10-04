Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\win 10\.gemini\antigravity-ide\brain\1798eacf-9dc6-41e4-bfdd-80c93508f555\.user_uploaded\media_1790769834705.jpg"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)
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

Make-SquareIcon 192 "f:\FaceStoneWebsite\scratch\test_icon_192.png" $false
Make-SquareIcon 512 "f:\FaceStoneWebsite\scratch\test_icon_512.png" $false
Make-SquareIcon 512 "f:\FaceStoneWebsite\scratch\test_maskable_512.png" $true

$bmp.Dispose()
