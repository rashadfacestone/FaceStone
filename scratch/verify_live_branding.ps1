$ready = $false
for ($i = 0; $i -lt 15; $i++) {
    try {
        $res = Invoke-WebRequest -Uri 'http://127.0.0.1:5124' -UseBasicParsing -TimeoutSec 2
        if ($res.StatusCode -eq 200) { $ready = $true; break }
    } catch {}
    Start-Sleep -Milliseconds 600
}
Write-Host "Server ready: $ready"
if ($ready) {
    # Check that logo is served
    $imgRes = Invoke-WebRequest -Uri 'http://127.0.0.1:5124/images/facestone-logo-transparent.png' -UseBasicParsing
    Write-Host "Logo transparent status: $($imgRes.StatusCode), size: $($imgRes.RawContentLength)"
    
    # Check that index.html contains FaceStone
    $html = $res.Content
    $hasTitle = $html -match '<title>FaceStone</title>'
    $hasDeptBrand = $html -match 'dept-brand-logo-img'
    $hasAuthBrand = $html -match 'auth-brand-logo-img'
    Write-Host "Title is FaceStone: $hasTitle"
    Write-Host "Has dept-brand-logo-img: $hasDeptBrand"
    Write-Host "Has auth-brand-logo-img: $hasAuthBrand"
}
