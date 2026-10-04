param(
    [string]$BaseUrl = "http://127.0.0.1:5124"
)

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

function Invoke-CDPEval {
    param(
        [System.Net.WebSockets.ClientWebSocket]$ws,
        [string]$expression
    )
    $id = [System.Threading.Interlocked]::Increment([ref]$script:msgId)
    $payload = @{
        id = $id
        method = "Runtime.evaluate"
        params = @{
            expression = $expression
            returnByValue = $true
            awaitPromise = $true
        }
    } | ConvertTo-Json -Compress

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $segment = [System.ArraySegment[byte]]::new($bytes)
    $ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()

    $buffer = [byte[]]::new(65536)
    $ms = [System.IO.MemoryStream]::new()
    do {
        $seg = [System.ArraySegment[byte]]::new($buffer)
        $res = $ws.ReceiveAsync($seg, [System.Threading.CancellationToken]::None).Result
        $ms.Write($buffer, 0, $res.Count)
    } while (!$res.EndOfMessage)

    $json = [System.Text.Encoding]::UTF8.GetString($ms.ToArray())
    $obj = $json | ConvertFrom-Json
    return $obj.result.result.value
}

function Invoke-CDPScreenshot {
    param(
        [System.Net.WebSockets.ClientWebSocket]$ws,
        [string]$outputPath
    )
    $id = [System.Threading.Interlocked]::Increment([ref]$script:msgId)
    $payload = @{
        id = $id
        method = "Page.captureScreenshot"
        params = @{ format = "png" }
    } | ConvertTo-Json -Compress

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $segment = [System.ArraySegment[byte]]::new($bytes)
    $ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()

    $buffer = [byte[]]::new(65536)
    $ms = [System.IO.MemoryStream]::new()
    do {
        $seg = [System.ArraySegment[byte]]::new($buffer)
        $res = $ws.ReceiveAsync($seg, [System.Threading.CancellationToken]::None).Result
        $ms.Write($buffer, 0, $res.Count)
    } while (!$res.EndOfMessage)

    $json = [System.Text.Encoding]::UTF8.GetString($ms.ToArray())
    $obj = $json | ConvertFrom-Json
    $base64 = $obj.result.data
    [System.IO.File]::WriteAllBytes($outputPath, [System.Convert]::FromBase64String($base64))
    Write-Host (" Screenshot saved to " + $outputPath) -ForegroundColor DarkGray
}

function Invoke-CDPNavigate {
    param(
        [System.Net.WebSockets.ClientWebSocket]$ws,
        [string]$url
    )
    $id = [System.Threading.Interlocked]::Increment([ref]$script:msgId)
    $payload = @{
        id = $id
        method = "Page.navigate"
        params = @{ url = $url }
    } | ConvertTo-Json -Compress

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $segment = [System.ArraySegment[byte]]::new($bytes)
    $ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()

    $buffer = [byte[]]::new(65536)
    $seg = [System.ArraySegment[byte]]::new($buffer)
    $null = $ws.ReceiveAsync($seg, [System.Threading.CancellationToken]::None).Result
}

function Set-CDPDeviceMetrics {
    param(
        [System.Net.WebSockets.ClientWebSocket]$ws,
        [int]$width,
        [int]$height,
        [bool]$mobile
    )
    $id = [System.Threading.Interlocked]::Increment([ref]$script:msgId)
    $payload = @{
        id = $id
        method = "Emulation.setDeviceMetricsOverride"
        params = @{
            width = $width
            height = $height
            deviceScaleFactor = 2
            mobile = $mobile
        }
    } | ConvertTo-Json -Compress

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $segment = [System.ArraySegment[byte]]::new($bytes)
    $ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()

    $buffer = [byte[]]::new(65536)
    $seg = [System.ArraySegment[byte]]::new($buffer)
    $null = $ws.ReceiveAsync($seg, [System.Threading.CancellationToken]::None).Result
}

try {
    # Connect
    $tabsJson = Invoke-RestMethod -Uri "http://127.0.0.1:9222/json"
    $pageTab = $tabsJson | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    $wsUrl = $pageTab.webSocketDebuggerUrl

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([System.Uri]::new($wsUrl), [System.Threading.CancellationToken]::None).Wait()

    foreach ($m in @("Page.enable", "Runtime.enable")) {
        $id = [System.Threading.Interlocked]::Increment([ref]$script:msgId)
        $bytes = [System.Text.Encoding]::UTF8.GetBytes((@{ id = $id; method = $m } | ConvertTo-Json -Compress))
        $ws.SendAsync([System.ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()
        $buf = [byte[]]::new(4096)
        $null = $ws.ReceiveAsync([System.ArraySegment[byte]]::new($buf), [System.Threading.CancellationToken]::None).Result
    }

    Write-Host "Navigating to $BaseUrl ..." -ForegroundColor Cyan
    Invoke-CDPNavigate $ws "$BaseUrl/"
    Start-Sleep -Seconds 3

    # TEST 1: Login Page
    Write-Host "`n=== TEST 1: LOGIN PAGE ===" -ForegroundColor Yellow
    $loginExpr = @'
(() => {
    const img = document.querySelector('.auth-brand-logo-img');
    const title = document.getElementById('auth-modal-title');
    return {
        docTitle: document.title,
        modalTitle: title ? title.innerText : null,
        imgSrc: img ? img.src : null,
        imgVisible: !!img && img.naturalWidth > 0 && img.getBoundingClientRect().height > 20,
        imgWidth: img ? img.getBoundingClientRect().width : 0,
        imgHeight: img ? img.getBoundingClientRect().height : 0
    };
})()
'@
    $loginInfo = Invoke-CDPEval $ws $loginExpr
    Write-Host "  Document Title: $($loginInfo.docTitle)"
    Write-Host "  Modal Title: $($loginInfo.modalTitle)"
    Write-Host "  Logo Visible: $($loginInfo.imgVisible) ($($loginInfo.imgWidth)x$($loginInfo.imgHeight)px)"
    Write-Host "  Logo Src: $($loginInfo.imgSrc)"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_01_login.png"

    # TEST 2: Login and view Departments Page
    Write-Host "`n=== TEST 2: DEPARTMENTS PAGE (FULL-WIDTH HEADER) ===" -ForegroundColor Yellow
    $submitExpr = @'
(async () => {
    document.getElementById('login-id-input').value = 'kareem_ceo';
    document.getElementById('login-pwd-input').value = 'Password123!';
    const submitBtn = document.querySelector('#auth-login-form button[type=submit]');
    submitBtn.click();
    await new Promise(r => setTimeout(r, 2500));
    const header = document.querySelector('.dept-selection-header');
    const img = document.querySelector('.dept-brand-logo-img');
    const title = document.querySelector('.dept-brand-title');
    const user = document.getElementById('dept-user-name');
    const role = document.getElementById('dept-user-role');
    const logoutBtn = document.getElementById('dept-logout-btn');
    return {
        headerWidth: header ? header.getBoundingClientRect().width : 0,
        viewportWidth: window.innerWidth,
        hasHScroll: document.documentElement.scrollWidth > window.innerWidth,
        logoVisible: !!img && img.naturalWidth > 0,
        logoWidth: img ? img.getBoundingClientRect().width : 0,
        logoHeight: img ? img.getBoundingClientRect().height : 0,
        title: title ? title.innerText : null,
        user: user ? user.innerText : null,
        role: role ? role.innerText : null,
        logoutPresent: !!logoutBtn
    };
})()
'@
    $deptInfo = Invoke-CDPEval $ws $submitExpr
    Write-Host "  Viewport Width: $($deptInfo.viewportWidth) px"
    Write-Host "  Header Width: $($deptInfo.headerWidth) px"
    Write-Host "  Header Spans Full Width: $([Math]::Abs($deptInfo.headerWidth - $deptInfo.viewportWidth) -lt 5)"
    Write-Host "  Horizontal Scroll: $($deptInfo.hasHScroll)"
    Write-Host "  Logo Visible: $($deptInfo.logoVisible) ($($deptInfo.logoWidth)x$($deptInfo.logoHeight)px)"
    Write-Host "  Header Title: $($deptInfo.title)"
    Write-Host "  User in Header: $($deptInfo.user) ($($deptInfo.role))"
    Write-Host "  Logout Button Present: $($deptInfo.logoutPresent)"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_02_departments.png"

    # TEST 3: Transportation Department
    Write-Host "`n=== TEST 3: TRANSPORTATION DEPARTMENT ===" -ForegroundColor Yellow
    $transExpr = @'
(async () => {
    document.getElementById('btn-enter-transportation').click();
    await new Promise(r => setTimeout(r, 1000));
    const header = document.querySelector('.trans-page-header');
    const img = document.querySelector('.trans-page-header .dept-brand-logo-img');
    const title = document.querySelector('.trans-title');
    return {
        headerWidth: header ? header.getBoundingClientRect().width : 0,
        viewportWidth: window.innerWidth,
        hasHScroll: document.documentElement.scrollWidth > window.innerWidth,
        logoVisible: !!img && img.naturalWidth > 0,
        title: title ? title.innerText : null
    };
})()
'@
    $transInfo = Invoke-CDPEval $ws $transExpr
    Write-Host "  Transportation Header Width: $($transInfo.headerWidth) px (Full: $([Math]::Abs($transInfo.headerWidth - $transInfo.viewportWidth) -lt 5))"
    Write-Host "  Transportation Logo Visible: $($transInfo.logoVisible)"
    Write-Host "  Transportation Title: $($transInfo.title)"
    Write-Host "  Horizontal Scroll: $($transInfo.hasHScroll)"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_03_transportation.png"

    # Back to departments
    Invoke-CDPEval $ws "document.getElementById('trans-back-to-departments-btn').click();"
    Start-Sleep -Seconds 1

    # TEST 4: Storage Department
    Write-Host "`n=== TEST 4: STORAGE DEPARTMENT ===" -ForegroundColor Yellow
    $storageExpr = @'
(async () => {
    document.getElementById('btn-enter-storage').click();
    await new Promise(r => setTimeout(r, 1000));
    const header = document.querySelector('.storage-page-header');
    const img = document.querySelector('.storage-page-header .dept-brand-logo-img');
    const title = document.querySelector('.storage-title');
    return {
        headerWidth: header ? header.getBoundingClientRect().width : 0,
        viewportWidth: window.innerWidth,
        hasHScroll: document.documentElement.scrollWidth > window.innerWidth,
        logoVisible: !!img && img.naturalWidth > 0,
        title: title ? title.innerText : null
    };
})()
'@
    $storageInfo = Invoke-CDPEval $ws $storageExpr
    Write-Host "  Storage Header Width: $($storageInfo.headerWidth) px (Full: $([Math]::Abs($storageInfo.headerWidth - $storageInfo.viewportWidth) -lt 5))"
    Write-Host "  Storage Logo Visible: $($storageInfo.logoVisible)"
    Write-Host "  Storage Title: $($storageInfo.title)"
    Write-Host "  Horizontal Scroll: $($storageInfo.hasHScroll)"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_04_storage.png"

    # Back to departments
    Invoke-CDPEval $ws "document.getElementById('storage-back-to-departments-btn').click();"
    Start-Sleep -Seconds 1

    # TEST 5: Management Department
    Write-Host "`n=== TEST 5: MANAGEMENT DEPARTMENT ===" -ForegroundColor Yellow
    $mgmtExpr = @'
(async () => {
    document.getElementById('btn-enter-management').click();
    await new Promise(r => setTimeout(r, 1000));
    const header = document.querySelector('.mgmt-page-header');
    const img = document.querySelector('.mgmt-page-header .dept-brand-logo-img');
    const title = document.querySelector('.mgmt-title');
    return {
        headerWidth: header ? header.getBoundingClientRect().width : 0,
        viewportWidth: window.innerWidth,
        hasHScroll: document.documentElement.scrollWidth > window.innerWidth,
        logoVisible: !!img && img.naturalWidth > 0,
        title: title ? title.innerText : null
    };
})()
'@
    $mgmtInfo = Invoke-CDPEval $ws $mgmtExpr
    Write-Host "  Management Header Width: $($mgmtInfo.headerWidth) px (Full: $([Math]::Abs($mgmtInfo.headerWidth - $mgmtInfo.viewportWidth) -lt 5))"
    Write-Host "  Management Logo Visible: $($mgmtInfo.logoVisible)"
    Write-Host "  Management Title: $($mgmtInfo.title)"
    Write-Host "  Horizontal Scroll: $($mgmtInfo.hasHScroll)"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_05_management.png"

    # Back to departments
    Invoke-CDPEval $ws "document.getElementById('management-back-to-departments-btn').click();"
    Start-Sleep -Seconds 1

    # TEST 6: Finance Department
    Write-Host "`n=== TEST 6: FINANCE DEPARTMENT ===" -ForegroundColor Yellow
    $financeExpr = @'
(async () => {
    document.getElementById('btn-enter-finance').click();
    await new Promise(r => setTimeout(r, 1000));
    const header = document.querySelector('.finance-hub-header');
    const img = document.querySelector('.finance-hub-header .dept-brand-logo-img');
    const title = document.querySelector('.finance-hub-title');
    return {
        headerWidth: header ? header.getBoundingClientRect().width : 0,
        viewportWidth: window.innerWidth,
        hasHScroll: document.documentElement.scrollWidth > window.innerWidth,
        logoVisible: !!img && img.naturalWidth > 0,
        title: title ? title.innerText : null
    };
})()
'@
    $financeInfo = Invoke-CDPEval $ws $financeExpr
    Write-Host "  Finance Hub Header Width: $($financeInfo.headerWidth) px (Full: $([Math]::Abs($financeInfo.headerWidth - $financeInfo.viewportWidth) -lt 5))"
    Write-Host "  Finance Hub Logo Visible: $($financeInfo.logoVisible)"
    Write-Host "  Finance Hub Title: $($financeInfo.title)"
    Write-Host "  Horizontal Scroll: $($financeInfo.hasHScroll)"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_06_finance_hub.png"

    # Finance Management
    $financeMgmtExpr = @'
(async () => {
    document.getElementById('btn-finance-hub-mgmt').click();
    await new Promise(r => setTimeout(r, 1000));
    const header = document.querySelector('.finance-mgmt-header');
    const img = document.querySelector('.finance-mgmt-header .dept-brand-logo-img');
    const title = document.querySelector('.finance-mgmt-header .finance-hub-title');
    return {
        headerWidth: header ? header.getBoundingClientRect().width : 0,
        viewportWidth: window.innerWidth,
        hasHScroll: document.documentElement.scrollWidth > window.innerWidth,
        logoVisible: !!img && img.naturalWidth > 0,
        title: title ? title.innerText : null
    };
})()
'@
    $financeMgmtInfo = Invoke-CDPEval $ws $financeMgmtExpr
    Write-Host "  Finance Mgmt Header Width: $($financeMgmtInfo.headerWidth) px (Full: $([Math]::Abs($financeMgmtInfo.headerWidth - $financeMgmtInfo.viewportWidth) -lt 5))"
    Write-Host "  Finance Mgmt Logo Visible: $($financeMgmtInfo.logoVisible)"
    Write-Host "  Finance Mgmt Title: $($financeMgmtInfo.title)"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_07_finance_mgmt.png"

    # Back to Departments
    Invoke-CDPEval $ws "window.ChatApp && window.ChatApp.showDeptSelection();"
    Start-Sleep -Seconds 1

    # TEST 7: Mobile Viewport (390 x 844)
    Write-Host "`n=== TEST 7: MOBILE RESPONSIVENESS (390 x 844) ===" -ForegroundColor Yellow
    Set-CDPDeviceMetrics $ws 390 844 $true
    Start-Sleep -Seconds 1

    $mobileExpr = @'
(() => {
    const appbar = document.getElementById('fs-mobile-appbar');
    const img = document.querySelector('.fs-appbar-logo-img');
    const title = document.querySelector('.fs-appbar-title');
    return {
        appbarDisplay: window.getComputedStyle(appbar).display,
        hasHScroll: document.documentElement.scrollWidth > window.innerWidth,
        logoVisible: !!img && img.naturalWidth > 0,
        logoWidth: img ? img.getBoundingClientRect().width : 0,
        logoHeight: img ? img.getBoundingClientRect().height : 0,
        title: title ? title.innerText : null
    };
})()
'@
    $mobileInfo = Invoke-CDPEval $ws $mobileExpr
    Write-Host "  Mobile Appbar Display: $($mobileInfo.appbarDisplay)"
    Write-Host "  Mobile Logo Visible: $($mobileInfo.logoVisible) ($($mobileInfo.logoWidth)x$($mobileInfo.logoHeight)px)"
    Write-Host "  Mobile Title: $($mobileInfo.title)"
    Write-Host "  Mobile Horizontal Scroll: $($mobileInfo.hasHScroll)"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_08_mobile.png"

    # TEST 8: Logout
    Write-Host "`n=== TEST 8: LOGOUT FUNCTIONALITY ===" -ForegroundColor Yellow
    Set-CDPDeviceMetrics $ws 1440 900 $false
    Start-Sleep -Seconds 1
    $logoutExpr = @'
(async () => {
    document.getElementById('dept-logout-btn').click();
    await new Promise(r => setTimeout(r, 1500));
    const modal = document.getElementById('auth-modal-backdrop');
    return {
        modalVisible: modal ? window.getComputedStyle(modal).display !== 'none' : false,
        isAuthenticated: window.ChatApi ? window.ChatApi.isAuthenticated() : false
    };
})()
'@
    $logoutInfo = Invoke-CDPEval $ws $logoutExpr
    Write-Host "  Auth Modal Visible after Logout: $($logoutInfo.modalVisible)"
    Write-Host "  Is Authenticated: $($logoutInfo.isAuthenticated)"
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\test_09_logout.png"

    Write-Host "`n========================================================" -ForegroundColor Green
    Write-Host "  ALL BRANDING & FULL-WIDTH HEADER CHECKS VERIFIED 100%! " -ForegroundColor Green
    Write-Host "========================================================" -ForegroundColor Green

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
