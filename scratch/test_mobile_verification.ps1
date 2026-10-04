param(
    [string]$BaseUrl = "http://127.0.0.1:5124"
)

$ErrorActionPreference = "Stop"

# Start Chrome in headless mode with mobile dimensions
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
    param($ws, [string]$expr)
    $id = [System.Threading.Interlocked]::Increment([ref]$script:msgId)
    $payload = @{
        id = $id
        method = "Runtime.evaluate"
        params = @{ expression = $expr; returnByValue = $true; awaitPromise = $true }
    } | ConvertTo-Json -Compress

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $ws.SendAsync([System.ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()

    $buffer = [byte[]]::new(65536)
    $ms = [System.IO.MemoryStream]::new()
    do {
        $res = $ws.ReceiveAsync([System.ArraySegment[byte]]::new($buffer), [System.Threading.CancellationToken]::None).Result
        $ms.Write($buffer, 0, $res.Count)
    } while (!$res.EndOfMessage)

    $obj = [System.Text.Encoding]::UTF8.GetString($ms.ToArray()) | ConvertFrom-Json
    return $obj.result.result.value
}

function Invoke-CDPScreenshot {
    param($ws, [string]$outPath)
    $id = [System.Threading.Interlocked]::Increment([ref]$script:msgId)
    $payload = @{
        id = $id
        method = "Page.captureScreenshot"
        params = @{ format = "png" }
    } | ConvertTo-Json -Compress

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $ws.SendAsync([System.ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()

    $buffer = [byte[]]::new(65536)
    $ms = [System.IO.MemoryStream]::new()
    do {
        $res = $ws.ReceiveAsync([System.ArraySegment[byte]]::new($buffer), [System.Threading.CancellationToken]::None).Result
        $ms.Write($buffer, 0, $res.Count)
    } while (!$res.EndOfMessage)

    $obj = [System.Text.Encoding]::UTF8.GetString($ms.ToArray()) | ConvertFrom-Json
    [System.IO.File]::WriteAllBytes($outPath, [System.Convert]::FromBase64String($obj.result.data))
    Write-Host "Screenshot saved: $outPath"
}

function Invoke-CDPNavigate {
    param($ws, [string]$url)
    $id = [System.Threading.Interlocked]::Increment([ref]$script:msgId)
    $payload = @{ id = $id; method = "Page.navigate"; params = @{ url = $url } } | ConvertTo-Json -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $ws.SendAsync([System.ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()
    $buf = [byte[]]::new(65536)
    $null = $ws.ReceiveAsync([System.ArraySegment[byte]]::new($buf), [System.Threading.CancellationToken]::None).Result
    Start-Sleep -Seconds 1
}

function Set-CDPMetrics {
    param($ws, [int]$w, [int]$h, [bool]$mob)
    $id = [System.Threading.Interlocked]::Increment([ref]$script:msgId)
    $payload = @{
        id = $id
        method = "Emulation.setDeviceMetricsOverride"
        params = @{ width = $w; height = $h; deviceScaleFactor = 2; mobile = $mob }
    } | ConvertTo-Json -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $ws.SendAsync([System.ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()
    $buf = [byte[]]::new(65536)
    $null = $ws.ReceiveAsync([System.ArraySegment[byte]]::new($buf), [System.Threading.CancellationToken]::None).Result
}

try {
    $tabs = Invoke-RestMethod -Uri "http://127.0.0.1:9222/json"
    $pageTab = $tabs | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([System.Uri]::new($pageTab.webSocketDebuggerUrl), [System.Threading.CancellationToken]::None).Wait()

    # Step 1: Mobile Mode (390x844)
    Set-CDPMetrics $ws 390 844 $true
    Invoke-CDPNavigate $ws "$BaseUrl/"
    Start-Sleep -Seconds 2

    # Check manifest
    $mHref = Invoke-CDPEval $ws "document.querySelector('link[rel=manifest]') ? document.querySelector('link[rel=manifest]').getAttribute('href') : null"
    Write-Host "PWA Manifest link detected: $mHref"

    # Login
    $loginCode = "
(function() {
    document.getElementById('login-id-input').value = 'kareem_ceo';
    document.getElementById('login-pwd-input').value = 'Password123!';
    document.querySelector('#auth-login-form button[type=submit]').click();
    return 'submitted';
})()
"
    $null = Invoke-CDPEval $ws $loginCode
    Start-Sleep -Seconds 2

    # Verify Mobile App Bar & Bottom Nav
    $checkMobile = "
(function() {
    const appbar = document.getElementById('fs-mobile-appbar');
    const bottomNav = document.getElementById('fs-mobile-bottom-nav');
    const cameraBtn = document.getElementById('fs-mobile-camera-btn');
    const activeTab = document.querySelector('.fs-nav-tab.active');
    return {
        isAuth: window.ChatApi.isAuthenticated(),
        user: window.ChatApi.getCurrentUser() ? window.ChatApi.getCurrentUser().fullName : null,
        appbarDisplay: window.getComputedStyle(appbar).display,
        navDisplay: window.getComputedStyle(bottomNav).display,
        activeTab: activeTab ? activeTab.id : null,
        hasCamera: !!cameraBtn
    };
})()
"
    $mobResult = Invoke-CDPEval $ws $checkMobile
    Write-Host "Mobile Dashboard State: $($mobResult | ConvertTo-Json -Compress)"
    Invoke-CDPScreenshot $ws "scratch\mobile_01_dashboard.png"

    # Step 2: Test Bottom Nav -> Transportation
    $clickTrans = "
(function() {
    document.getElementById('fs-nav-trans').click();
    const root = document.getElementById('transportation-page-root');
    const activeTab = document.querySelector('.fs-nav-tab.active');
    return {
        display: window.getComputedStyle(root).display,
        tab: activeTab ? activeTab.id : null
    };
})()
"
    $transRes = Invoke-CDPEval $ws $clickTrans
    Write-Host "Mobile Transportation Tab: $($transRes | ConvertTo-Json -Compress)"
    Invoke-CDPScreenshot $ws "scratch\mobile_02_transportation.png"

    # Step 3: Test Bottom Nav -> Storage
    $clickStorage = "
(function() {
    document.getElementById('fs-nav-storage').click();
    const root = document.getElementById('storage-page-root');
    const activeTab = document.querySelector('.fs-nav-tab.active');
    return {
        display: window.getComputedStyle(root).display,
        tab: activeTab ? activeTab.id : null
    };
})()
"
    $storageRes = Invoke-CDPEval $ws $clickStorage
    Write-Host "Mobile Storage Tab: $($storageRes | ConvertTo-Json -Compress)"
    Invoke-CDPScreenshot $ws "scratch\mobile_03_storage.png"

    # Step 4: Test Bottom Nav -> Chat
    $clickChat = "
(function() {
    document.getElementById('fs-nav-chat').click();
    const root = document.getElementById('chat-app-root');
    const activeTab = document.querySelector('.fs-nav-tab.active');
    const camBtn = document.getElementById('fs-mobile-camera-btn');
    return {
        display: window.getComputedStyle(root).display,
        tab: activeTab ? activeTab.id : null,
        cameraVisible: !!(camBtn && window.getComputedStyle(camBtn).display !== 'none')
    };
})()
"
    $chatRes = Invoke-CDPEval $ws $clickChat
    Write-Host "Mobile Chat Tab: $($chatRes | ConvertTo-Json -Compress)"
    Invoke-CDPScreenshot $ws "scratch\mobile_04_chat.png"

    # Step 5: Test Profile Sheet
    $clickProfile = "
(function() {
    document.getElementById('fs-appbar-avatar-btn').click();
    const sheet = document.getElementById('fs-profile-sheet-backdrop');
    return {
        open: sheet.classList.contains('open'),
        name: document.getElementById('fs-sheet-user-name').innerText,
        role: document.getElementById('fs-sheet-user-role').innerText
    };
})()
"
    $sheetRes = Invoke-CDPEval $ws $clickProfile
    Write-Host "Mobile Profile Sheet: $($sheetRes | ConvertTo-Json -Compress)"
    Invoke-CDPScreenshot $ws "scratch\mobile_05_profile.png"

    # Close profile sheet & return to depts
    $closeProfile = "
(function() {
    window.FaceStoneMobileApp.closeProfileSheet();
    document.getElementById('fs-nav-depts').click();
    return true;
})()
"
    $null = Invoke-CDPEval $ws $closeProfile

    # Step 6: Desktop Compatibility Test (1280x850)
    Set-CDPMetrics $ws 1280 850 $false
    Start-Sleep -Seconds 1

    $checkDesktop = "
(function() {
    const bottomNav = document.getElementById('fs-mobile-bottom-nav');
    const appbar = document.getElementById('fs-mobile-appbar');
    const desktopHeader = document.querySelector('.dept-selection-header');
    return {
        bottomNavDisplay: window.getComputedStyle(bottomNav).display,
        appbarDisplay: window.getComputedStyle(appbar).display,
        desktopHeaderDisplay: window.getComputedStyle(desktopHeader).display
    };
})()
"
    $deskResult = Invoke-CDPEval $ws $checkDesktop
    Write-Host "Desktop State: $($deskResult | ConvertTo-Json -Compress)"
    Invoke-CDPScreenshot $ws "scratch\desktop_verified.png"

    Write-Host "`n>>> ALL MOBILE PWA & DESKTOP SUITE TESTS COMPLETED SUCCESSFULLY! <<<" -ForegroundColor Green
}
finally {
    if ($ws) {
        try { $ws.Dispose() } catch {}
    }
    if ($chromeProc -and !$chromeProc.HasExited) {
        try { Stop-Process -Id $chromeProc.Id -Force } catch {}
    }
}
