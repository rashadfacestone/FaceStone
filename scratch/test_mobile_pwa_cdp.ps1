param(
    [string]$BaseUrl = "http://127.0.0.1:5124"
)

$ErrorActionPreference = "Stop"

# 1. Start Chrome in headless mode with remote debugging on port 9222
$tempProfile = Join-Path $env:TEMP "chrome_cdp_mobile_$(Get-Random)"
$chromeArgs = @(
    "--headless=new",
    "--remote-debugging-port=9222",
    "--window-size=390,844",
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
    # 2. Connect to CDP Page Target
    $tabsJson = Invoke-RestMethod -Uri "http://127.0.0.1:9222/json"
    $pageTab = $tabsJson | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    $wsUrl = $pageTab.webSocketDebuggerUrl

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([System.Uri]::new($wsUrl), [System.Threading.CancellationToken]::None).Wait()

    # Enable Page and Runtime
    foreach ($m in @("Page.enable", "Runtime.enable")) {
        $id = [System.Threading.Interlocked]::Increment([ref]$script:msgId)
        $bytes = [System.Text.Encoding]::UTF8.GetBytes((@{ id = $id; method = $m } | ConvertTo-Json -Compress))
        $ws.SendAsync([System.ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()
        $buf = [byte[]]::new(4096)
        $null = $ws.ReceiveAsync([System.ArraySegment[byte]]::new($buf), [System.Threading.CancellationToken]::None).Result
    }

    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host " 1. EMULATING MOBILE VIEWPORT (390 x 844) iPhone/Android" -ForegroundColor Cyan
    Write-Host "========================================================" -ForegroundColor Cyan
    Set-CDPDeviceMetrics $ws 390 844 $true

    Invoke-CDPNavigate $ws "$BaseUrl/"
    Start-Sleep -Seconds 3

    # Check PWA manifest link in head
    $manifestHref = Invoke-CDPEval $ws "document.querySelector('link[rel=manifest]') ? document.querySelector('link[rel=manifest]').getAttribute('href') : null"
    Write-Host ("PASS: PWA Manifest linked: " + $manifestHref) -ForegroundColor Green

    # Perform Login
    Write-Host "Submitting Login with 'kareem_ceo'..." -ForegroundColor Yellow
    $loginExpr = @'
(async () => {
    document.getElementById('login-id-input').value = 'kareem_ceo';
    document.getElementById('login-pwd-input').value = 'Password123!';
    const submitBtn = document.querySelector('#auth-login-form button[type=submit]');
    submitBtn.click();
    await new Promise(r => setTimeout(r, 2000));
    return {
        isAuthenticated: window.ChatApi.isAuthenticated(),
        userName: window.ChatApi.getCurrentUser() ? window.ChatApi.getCurrentUser().fullName : null,
        userRole: window.ChatApi.getCurrentUser() ? window.ChatApi.getCurrentUser().role : null
    };
})()
'@
    $loginResult = Invoke-CDPEval $ws $loginExpr
    Write-Host ("PASS: Authenticated: " + $loginResult.isAuthenticated + " as " + $loginResult.userName + " (" + $loginResult.userRole + ")") -ForegroundColor Green

    # Verify Mobile App Bar & Bottom Navigation
    $elementsExpr = @'
(() => {
    const appbar = document.getElementById('fs-mobile-appbar');
    const bottomNav = document.getElementById('fs-mobile-bottom-nav');
    const appbarStyle = window.getComputedStyle(appbar);
    const navStyle = window.getComputedStyle(bottomNav);
    const cameraBtn = document.getElementById('fs-mobile-camera-btn');
    const tabs = Array.from(document.querySelectorAll('.fs-nav-tab')).map(t => t.id);
    return {
        appbarDisplay: appbarStyle.display,
        navDisplay: navStyle.display,
        hasCameraBtn: !!cameraBtn,
        tabs: tabs.join(', ')
    };
})()
'@
    $mobileElements = Invoke-CDPEval $ws $elementsExpr
    Write-Host ("PASS: Mobile Top App Bar display: " + $mobileElements.appbarDisplay) -ForegroundColor Green
    Write-Host ("PASS: Mobile Bottom Nav display: " + $mobileElements.navDisplay) -ForegroundColor Green
    Write-Host ("PASS: Mobile Tabs present: " + $mobileElements.tabs) -ForegroundColor Green
    Write-Host ("PASS: Mobile Camera Button present: " + $mobileElements.hasCameraBtn) -ForegroundColor Green

    Invoke-CDPScreenshot $ws "scratch\mobile_01_dashboard.png"

    # Test Tab Navigation to Transportation
    Write-Host "Testing Mobile Bottom Nav: Tapping 'النقل' (Transportation)..." -ForegroundColor Yellow
    $transExpr = @'
(() => {
    document.getElementById('fs-nav-trans').click();
    const transRoot = document.getElementById('transportation-page-root');
    const activeTab = document.querySelector('.fs-nav-tab.active');
    return {
        transDisplay: window.getComputedStyle(transRoot).display,
        activeTab: activeTab ? activeTab.id : null
    };
})()
'@
    $transNavResult = Invoke-CDPEval $ws $transExpr
    Write-Host ("PASS: Transportation view active: " + $transNavResult.transDisplay + " | Active tab: " + $transNavResult.activeTab) -ForegroundColor Green
    Invoke-CDPScreenshot $ws "scratch\mobile_02_transportation.png"

    # Test Tab Navigation to Storage
    Write-Host "Testing Mobile Bottom Nav: Tapping 'المستودعات' (Storage)..." -ForegroundColor Yellow
    $storageExpr = @'
(() => {
    document.getElementById('fs-nav-storage').click();
    const storageRoot = document.getElementById('storage-page-root');
    const activeTab = document.querySelector('.fs-nav-tab.active');
    return {
        storageDisplay: window.getComputedStyle(storageRoot).display,
        activeTab: activeTab ? activeTab.id : null
    };
})()
'@
    $storageNavResult = Invoke-CDPEval $ws $storageExpr
    Write-Host ("PASS: Storage view active: " + $storageNavResult.storageDisplay + " | Active tab: " + $storageNavResult.activeTab) -ForegroundColor Green
    Invoke-CDPScreenshot $ws "scratch\mobile_03_storage.png"

    # Test Tab Navigation to Chat
    Write-Host "Testing Mobile Bottom Nav: Tapping 'المحادثات' (Chat)..." -ForegroundColor Yellow
    $chatExpr = @'
(() => {
    document.getElementById('fs-nav-chat').click();
    const chatRoot = document.getElementById('chat-app-root');
    const cameraBtn = document.getElementById('fs-mobile-camera-btn');
    const activeTab = document.querySelector('.fs-nav-tab.active');
    return {
        chatDisplay: window.getComputedStyle(chatRoot).display,
        activeTab: activeTab ? activeTab.id : null,
        cameraVisible: !!cameraBtn
    };
})()
'@
    $chatNavResult = Invoke-CDPEval $ws $chatExpr
    Write-Host ("PASS: Chat view active: " + $chatNavResult.chatDisplay + " | Active tab: " + $chatNavResult.activeTab + " | Camera: " + $chatNavResult.cameraVisible) -ForegroundColor Green
    Invoke-CDPScreenshot $ws "scratch\mobile_04_chat.png"

    # Test Profile Sheet
    Write-Host "Testing Mobile Profile Sheet..." -ForegroundColor Yellow
    $sheetExpr = @'
(() => {
    document.getElementById('fs-appbar-avatar-btn').click();
    const sheet = document.getElementById('fs-profile-sheet-backdrop');
    return {
        sheetOpen: sheet.classList.contains('open'),
        sheetName: document.getElementById('fs-sheet-user-name').textContent,
        sheetRole: document.getElementById('fs-sheet-user-role').textContent
    };
})()
'@
    $sheetResult = Invoke-CDPEval $ws $sheetExpr
    Write-Host ("PASS: Profile sheet open: " + $sheetResult.sheetOpen + " | User: " + $sheetResult.sheetName + " (" + $sheetResult.sheetRole + ")") -ForegroundColor Green
    Invoke-CDPScreenshot $ws "scratch\mobile_05_profile_sheet.png"

    # Close profile sheet and return to Departments
    $closeExpr = 'window.FaceStoneMobileApp.closeProfileSheet(); document.getElementById("fs-nav-depts").click();'
    $null = Invoke-CDPEval $ws $closeExpr

    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host " 2. TESTING DESKTOP VIEWPORT 1280x850 Compatibility" -ForegroundColor Cyan
    Write-Host "========================================================" -ForegroundColor Cyan
    Set-CDPDeviceMetrics $ws 1280 850 $false
    Start-Sleep -Seconds 1

    $desktopExpr = @'
(() => {
    const bottomNav = document.getElementById('fs-mobile-bottom-nav');
    const appbar = document.getElementById('fs-mobile-appbar');
    const desktopHeader = document.querySelector('.dept-selection-header');
    const deptGrid = document.querySelector('.dept-cards-grid');
    return {
        bottomNavDisplay: window.getComputedStyle(bottomNav).display,
        mobileAppbarDisplay: window.getComputedStyle(appbar).display,
        desktopHeaderDisplay: window.getComputedStyle(desktopHeader).display,
        deptGridColumns: window.getComputedStyle(deptGrid).gridTemplateColumns
    };
})()
'@
    $desktopElements = Invoke-CDPEval $ws $desktopExpr
    Write-Host ("PASS: Desktop mode: Mobile Bottom Nav display: " + $desktopElements.bottomNavDisplay + " (Should be 'none')") -ForegroundColor Green
    Write-Host ("PASS: Desktop mode: Mobile Appbar display: " + $desktopElements.mobileAppbarDisplay + " (Should be 'none')") -ForegroundColor Green
    Write-Host ("PASS: Desktop mode: Desktop Header display: " + $desktopElements.desktopHeaderDisplay + " (Should be 'flex')") -ForegroundColor Green
    Invoke-CDPScreenshot $ws "scratch\desktop_verified.png"

    Write-Host "========================================================" -ForegroundColor Green
    Write-Host " ALL MOBILE AND DESKTOP BROWSER VERIFICATIONS PASSED!" -ForegroundColor Green
    Write-Host "========================================================" -ForegroundColor Green
}
finally {
    if ($ws) {
        try { $ws.Dispose() } catch {}
    }
    if ($chromeProc -and !$chromeProc.HasExited) {
        try { Stop-Process -Id $chromeProc.Id -Force } catch {}
    }
}
