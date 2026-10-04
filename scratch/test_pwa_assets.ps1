$urls = @(
    'http://localhost:5124/manifest.json',
    'http://localhost:5124/manifest.webmanifest',
    'http://localhost:5124/sw.js',
    'http://localhost:5124/css/mobile-app.css',
    'http://localhost:5124/js/mobile-app.js',
    'http://localhost:5124/assets/icon-192.png'
)
foreach ($u in $urls) {
    try {
        $res = Invoke-WebRequest -Uri $u -Method Head -TimeoutSec 3
        Write-Output ("[OK] " + $u + " Status=" + $res.StatusCode + " ContentType=" + $res.Headers['Content-Type'])
    } catch {
        Write-Output ("[FAIL] " + $u + " Error=" + $_)
    }
}
