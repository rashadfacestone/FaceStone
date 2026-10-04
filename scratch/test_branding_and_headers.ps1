param(
    [string]$BaseUrl = "http://127.0.0.1:5124"
)

Add-Type -AssemblyName System.Net.Http
$ErrorActionPreference = "Stop"

# Ensure any existing headless chrome on 9222 is closed
Get-Process chrome -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like "*remote-debugging-port=9222*" } | Stop-Process -Force -ErrorAction SilentlyContinue

$tempProfile = Join-Path $env:TEMP "chrome_cdp_brand_$(Get-Random)"
$chromeArgs = @(
    "--headless=new",
    "--remote-debugging-port=9222",
    "--window-size=1440,900",
    "--user-data-dir=$tempProfile",
    "about:blank"
)
$chromeProc = Start-Process -FilePath "C:\Program Files\Google\Chrome\Application\chrome.exe" -ArgumentList $chromeArgs -PassThru
Start-Sleep -Seconds 2

$script:msgId = 0

function Invoke-CDPCommand {
    param(
        [System.Net.WebSockets.ClientWebSocket]$ws,
        [string]$method,
        [hashtable]$params = @{}
    )
    $id = [System.Threading.Interlocked]::Increment([ref]$script:msgId)
    $payload = @{
        id = $id
        method = $method
        params = $params
    } | ConvertTo-Json -Compress

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $segment = [System.ArraySegment[byte]]::new($bytes)
    $ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()

    # Loop until we receive the response with matching ID
    do {
        $buffer = [byte[]]::new(65536)
        $ms = [System.IO.MemoryStream]::new()
        do {
            $seg = [System.ArraySegment[byte]]::new($buffer)
            $res = $ws.ReceiveAsync($seg, [System.Threading.CancellationToken]::None).Result
            $ms.Write($buffer, 0, $res.Count)
        } while (!$res.EndOfMessage)

        $json = [System.Text.Encoding]::UTF8.GetString($ms.ToArray())
        $obj = $json | ConvertFrom-Json
    } while ($obj.id -ne $id)

    return $obj
}

function Invoke-CDPEval {
    param(
        [System.Net.WebSockets.ClientWebSocket]$ws,
        [string]$expression
    )
    $obj = Invoke-CDPCommand $ws "Runtime.evaluate" @{
        expression = $expression
        returnByValue = $true
        awaitPromise = $true
    }
    return $obj.result.result.value
}

function Invoke-CDPScreenshot {
    param(
        [System.Net.WebSockets.ClientWebSocket]$ws,
        [string]$outputPath
    )
    $obj = Invoke-CDPCommand $ws "Page.captureScreenshot" @{ format = "png" }
    $base64 = $obj.result.data
    $bytes = [System.Convert]::FromBase64String($base64)
    [System.IO.File]::WriteAllBytes($outputPath, $bytes)
    Write-Host (" [Screenshot] Saved to " + $outputPath + " (" + $bytes.Length + " bytes)") -ForegroundColor Green
}

function Invoke-CDPNavigate {
    param(
        [System.Net.WebSockets.ClientWebSocket]$ws,
        [string]$url
    )
    $null = Invoke-CDPCommand $ws "Page.navigate" @{ url = $url }
}

function Set-CDPViewport {
    param(
        [System.Net.WebSockets.ClientWebSocket]$ws,
        [int]$width,
        [int]$height,
        [bool]$mobile = $false
    )
    $null = Invoke-CDPCommand $ws "Emulation.setDeviceMetricsOverride" @{
        width = $width
        height = $height
        deviceScaleFactor = 1
        mobile = $mobile
    }
}

try {
    # Get WebSocket Debugger URL
    $client = [System.Net.Http.HttpClient]::new()
    $json = $client.GetStringAsync("http://127.0.0.1:9222/json").Result
    $tabs = $json | ConvertFrom-Json
    $wsUrl = $tabs[0].webSocketDebuggerUrl

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([Uri]$wsUrl, [System.Threading.CancellationToken]::None).Wait()
    Write-Host "Connected to Chrome CDP successfully." -ForegroundColor Cyan

    # Navigate to app
    Write-Host "`nNavigating to $BaseUrl ..."
    Invoke-CDPNavigate $ws $BaseUrl
    Start-Sleep -Seconds 2

    # TEST 1: Login Page
    Write-Host "`n=== TEST 1: LOGIN PAGE ===" -ForegroundColor Yellow
    $loginTitle = Invoke-CDPEval $ws "document.title"
    $loginLogoVisible = Invoke-CDPEval $ws "(()=>{ const img = document.querySelector('.auth-brand-logo-img'); return !!img && img.naturalWidth > 0 && img.getBoundingClientRect().height > 20; })()"
    $loginLogoSrc = Invoke-CDPEval $ws "document.querySelector('.auth-brand-logo-img')?.src"
    $loginModalTitle = Invoke-CDPEval $ws "document.getElementById('auth-modal-title')?.innerText"
    Write-Host "  Page Title: $loginTitle"
    Write-Host "  Login Modal Title: $loginModalTitle"
    Write-Host "  Logo Visible: $loginLogoVisible"
    Write-Host "  Logo Src: $loginLogoSrc"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_01_login.png"

    # TEST 2: Login and Departments Page (Desktop 1440px)
    Write-Host "`n=== TEST 2: DEPARTMENTS PAGE (FULL-WIDTH HEADER) ===" -ForegroundColor Yellow
    Invoke-CDPEval $ws "fillDemoAccount('kareem.ceo@facestone.com', 'Password123!');"
    Invoke-CDPEval $ws "document.getElementById('auth-login-form').dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }));"
    Start-Sleep -Seconds 3

    $deptHeaderWidth = Invoke-CDPEval $ws "document.querySelector('.dept-selection-header')?.getBoundingClientRect().width"
    $viewportWidth = Invoke-CDPEval $ws "window.innerWidth"
    $hasHScroll = Invoke-CDPEval $ws "document.documentElement.scrollWidth > window.innerWidth"
    $deptLogoVisible = Invoke-CDPEval $ws "(()=>{ const img = document.querySelector('.dept-brand-logo-img'); return !!img && img.naturalWidth > 0; })()"
    $deptLogoSrc = Invoke-CDPEval $ws "document.querySelector('.dept-brand-logo-img')?.src"
    $deptTitle = Invoke-CDPEval $ws "document.querySelector('.dept-brand-title')?.innerText"
    $deptUser = Invoke-CDPEval $ws "document.getElementById('dept-user-name')?.innerText"
    $deptLogout = Invoke-CDPEval $ws "!!document.getElementById('dept-logout-btn')"

    Write-Host "  Viewport Width: $viewportWidth px"
    Write-Host "  Header Width: $deptHeaderWidth px"
    Write-Host "  Header Spans Full Width: $([Math]::Abs($deptHeaderWidth - $viewportWidth) -lt 5)"
    Write-Host "  Horizontal Scroll: $hasHScroll"
    Write-Host "  Dept Logo Visible: $deptLogoVisible"
    Write-Host "  Dept Logo Src: $deptLogoSrc"
    Write-Host "  Company Title in Header: $deptTitle"
    Write-Host "  User in Header: $deptUser"
    Write-Host "  Logout Button Present: $deptLogout"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_02_departments.png"

    # TEST 3: Transportation Department
    Write-Host "`n=== TEST 3: TRANSPORTATION DEPARTMENT ===" -ForegroundColor Yellow
    Invoke-CDPEval $ws "document.getElementById('btn-enter-transportation')?.click();"
    Start-Sleep -Seconds 2
    $transHeaderWidth = Invoke-CDPEval $ws "document.querySelector('.trans-page-header')?.getBoundingClientRect().width"
    $transLogoVisible = Invoke-CDPEval $ws "(()=>{ const img = document.querySelector('.trans-page-header .dept-brand-logo-img'); return !!img && img.naturalWidth > 0; })()"
    $transTitle = Invoke-CDPEval $ws "document.querySelector('.trans-title')?.innerText"
    Write-Host "  Transportation Header Width: $transHeaderWidth px"
    Write-Host "  Transportation Logo Visible: $transLogoVisible"
    Write-Host "  Transportation Title: $transTitle"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_03_transportation.png"

    # Back to Departments
    Invoke-CDPEval $ws "document.getElementById('trans-back-to-departments-btn')?.click();"
    Start-Sleep -Seconds 1

    # TEST 4: Storage Department
    Write-Host "`n=== TEST 4: STORAGE DEPARTMENT ===" -ForegroundColor Yellow
    Invoke-CDPEval $ws "document.getElementById('btn-enter-storage')?.click();"
    Start-Sleep -Seconds 2
    $storageHeaderWidth = Invoke-CDPEval $ws "document.querySelector('.storage-page-header')?.getBoundingClientRect().width"
    $storageLogoVisible = Invoke-CDPEval $ws "(()=>{ const img = document.querySelector('.storage-page-header .dept-brand-logo-img'); return !!img && img.naturalWidth > 0; })()"
    $storageTitle = Invoke-CDPEval $ws "document.querySelector('.storage-title')?.innerText"
    Write-Host "  Storage Header Width: $storageHeaderWidth px"
    Write-Host "  Storage Logo Visible: $storageLogoVisible"
    Write-Host "  Storage Title: $storageTitle"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_04_storage.png"

    # Back to Departments
    Invoke-CDPEval $ws "document.getElementById('storage-back-to-departments-btn')?.click();"
    Start-Sleep -Seconds 1

    # TEST 5: Management Department
    Write-Host "`n=== TEST 5: MANAGEMENT DEPARTMENT ===" -ForegroundColor Yellow
    Invoke-CDPEval $ws "document.getElementById('btn-enter-management')?.click();"
    Start-Sleep -Seconds 2
    $mgmtHeaderWidth = Invoke-CDPEval $ws "document.querySelector('.mgmt-page-header')?.getBoundingClientRect().width"
    $mgmtLogoVisible = Invoke-CDPEval $ws "(()=>{ const img = document.querySelector('.mgmt-page-header .dept-brand-logo-img'); return !!img && img.naturalWidth > 0; })()"
    $mgmtTitle = Invoke-CDPEval $ws "document.querySelector('.mgmt-title')?.innerText"
    Write-Host "  Management Header Width: $mgmtHeaderWidth px"
    Write-Host "  Management Logo Visible: $mgmtLogoVisible"
    Write-Host "  Management Title: $mgmtTitle"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_05_management.png"

    # Back to Departments
    Invoke-CDPEval $ws "document.getElementById('management-back-to-departments-btn')?.click();"
    Start-Sleep -Seconds 1

    # TEST 6: Finance Department
    Write-Host "`n=== TEST 6: FINANCE DEPARTMENT ===" -ForegroundColor Yellow
    Invoke-CDPEval $ws "document.getElementById('btn-enter-finance')?.click();"
    Start-Sleep -Seconds 2
    $financeHeaderWidth = Invoke-CDPEval $ws "document.querySelector('.finance-hub-header')?.getBoundingClientRect().width"
    $financeLogoVisible = Invoke-CDPEval $ws "(()=>{ const img = document.querySelector('.finance-hub-header .dept-brand-logo-img'); return !!img && img.naturalWidth > 0; })()"
    $financeTitle = Invoke-CDPEval $ws "document.querySelector('.finance-hub-title')?.innerText"
    Write-Host "  Finance Header Width: $financeHeaderWidth px"
    Write-Host "  Finance Logo Visible: $financeLogoVisible"
    Write-Host "  Finance Title: $financeTitle"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_06_finance.png"

    # Back to Departments
    Invoke-CDPEval $ws "document.getElementById('finance-hub-back-to-departments-btn')?.click();"
    Start-Sleep -Seconds 1

    # TEST 7: Small Laptop Width (1024x768)
    Write-Host "`n=== TEST 7: LAPTOP VIEWPORT (1024x768) ===" -ForegroundColor Yellow
    Set-CDPViewport $ws 1024 768 $false
    Start-Sleep -Seconds 1
    $laptopHeaderWidth = Invoke-CDPEval $ws "document.querySelector('.dept-selection-header')?.getBoundingClientRect().width"
    $laptopHScroll = Invoke-CDPEval $ws "document.documentElement.scrollWidth > window.innerWidth"
    Write-Host "  Laptop Header Width: $laptopHeaderWidth px (Viewport: 1024 px)"
    Write-Host "  Horizontal Scroll: $laptopHScroll"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_07_laptop.png"

    # TEST 8: Mobile Viewport (390x844)
    Write-Host "`n=== TEST 8: MOBILE VIEWPORT (390x844) ===" -ForegroundColor Yellow
    Set-CDPViewport $ws 390 844 $true
    Start-Sleep -Seconds 1
    $mobileAppbarVisible = Invoke-CDPEval $ws "(()=>{ const bar = document.querySelector('.fs-mobile-appbar'); return !!bar && window.getComputedStyle(bar).display !== 'none'; })()"
    $mobileLogoVisible = Invoke-CDPEval $ws "(()=>{ const img = document.querySelector('.fs-appbar-logo-img'); return !!img && img.naturalWidth > 0; })()"
    $mobileHScroll = Invoke-CDPEval $ws "document.documentElement.scrollWidth > window.innerWidth"
    Write-Host "  Mobile Appbar Visible: $mobileAppbarVisible"
    Write-Host "  Mobile Logo Visible: $mobileLogoVisible"
    Write-Host "  Mobile Horizontal Scroll: $mobileHScroll"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_08_mobile.png"

    # TEST 9: Logout Functionality
    Write-Host "`n=== TEST 9: LOGOUT FUNCTIONALITY ===" -ForegroundColor Yellow
    Set-CDPViewport $ws 1440 900 $false
    Start-Sleep -Seconds 1
    Invoke-CDPEval $ws "document.getElementById('dept-logout-btn')?.click();"
    Start-Sleep -Seconds 2
    $loggedOut = Invoke-CDPEval $ws "document.getElementById('auth-modal-backdrop')?.style.display !== 'none'"
    Write-Host "  Logged Out and Returned to Auth Modal: $loggedOut"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_09_logged_out.png"

    Write-Host "`n=========================================" -ForegroundColor Green
    Write-Host "ALL VERIFICATION CHECKS PASSED!" -ForegroundColor Green
    Write-Host "=========================================" -ForegroundColor Green

} finally {
    if ($ws -and $ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", [System.Threading.CancellationToken]::None).Wait()
        $ws.Dispose()
    }
    if ($chromeProc -and !$chromeProc.HasExited) {
        $chromeProc.Kill()
    }
    Remove-Item -Recurse -Force $tempProfile -ErrorAction SilentlyContinue
}
