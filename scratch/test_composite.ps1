Add-Type -AssemblyName System.Drawing

$logo = [System.Drawing.Bitmap]::FromFile("f:\FaceStoneWebsite\wwwroot\assets\logo.png")

function Test-Composite([string]$name, [System.Drawing.Color]$bgColor) {
    $bmp = New-Object System.Drawing.Bitmap(1054, 251, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $brush = New-Object System.Drawing.SolidBrush($bgColor)
    $g.FillRectangle($brush, 0, 0, $bmp.Width, $bmp.Height)
    $brush.Dispose()
    
    $g.DrawImage($logo, 20, 20)
    $g.Dispose()
    
    $bmp.Save("f:\FaceStoneWebsite\scratch\$name.png", [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Created scratch\$name.png"
}

Test-Composite "test_on_light" ([System.Drawing.ColorTranslator]::FromHtml("#F1F5F9"))
Test-Composite "test_on_dark" ([System.Drawing.ColorTranslator]::FromHtml("#0B111E"))

$logo.Dispose()
