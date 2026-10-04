Add-Type -AssemblyName System.Drawing

function Resize-Image {
    param([string]$sourcePath, [string]$destPath, [int]$width, [int]$height, [bool]$pad = $false)
    $src = [System.Drawing.Image]::FromFile($sourcePath)
    $bmp = New-Object System.Drawing.Bitmap($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    if ($pad) {
        $brush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#0B111E'))
        $g.FillRectangle($brush, 0, 0, $width, $height)
        $brush.Dispose()
        $padW = [int]($width * 0.15)
        $padH = [int]($height * 0.15)
        $destRect = New-Object System.Drawing.Rectangle($padW, $padH, ($width - 2*$padW), ($height - 2*$padH))
        $g.DrawImage($src, $destRect)
    } else {
        $g.Clear([System.Drawing.Color]::Transparent)
        $destRect = New-Object System.Drawing.Rectangle(0, 0, $width, $height)
        $g.DrawImage($src, $destRect)
    }

    $g.Dispose()
    $src.Dispose()
    $bmp.Save($destPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "Generated $destPath"
}

Resize-Image 'F:\FaceStoneWebsite\wwwroot\assets\logo.png' 'F:\FaceStoneWebsite\wwwroot\assets\icon-192.png' 192 192 $false
Resize-Image 'F:\FaceStoneWebsite\wwwroot\assets\logo.png' 'F:\FaceStoneWebsite\wwwroot\assets\icon-512.png' 512 512 $false
Resize-Image 'F:\FaceStoneWebsite\wwwroot\assets\logo.png' 'F:\FaceStoneWebsite\wwwroot\assets\icon-maskable-512.png' 512 512 $true
Resize-Image 'F:\FaceStoneWebsite\wwwroot\assets\logo.png' 'F:\FaceStoneWebsite\wwwroot\assets\apple-touch-icon.png' 180 180 $false
