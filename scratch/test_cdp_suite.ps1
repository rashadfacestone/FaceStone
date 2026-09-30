param(
    [string]$BaseUrl = "http://127.0.0.1:5124"
)

$ErrorActionPreference = "Stop"

# 1. Start Chrome with remote debugging
$tempProfile = Join-Path $env:TEMP "chrome_cdp_profile_$(Get-Random)"
$chromeArgs = @(
    "--headless=new",
    "--remote-debugging-port=9222",
    "--window-size=1280,900",
    "--user-data-dir=$tempProfile",
    "about:blank"
)
$chromeProc = Start-Process -FilePath "C:\Program Files\Google\Chrome\Application\chrome.exe" -ArgumentList $chromeArgs -PassThru
Start-Sleep -Seconds 2

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

    # Read response
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
    Start-Sleep -Milliseconds 1000
}

try {
    $script:msgId = 0
    # Connect to first tab
    $tabs = Invoke-RestMethod -Uri "http://127.0.0.1:9222/json"
    $targetTab = $tabs | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    $wsUrl = $targetTab.webSocketDebuggerUrl

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([System.Uri]::new($wsUrl), [System.Threading.CancellationToken]::None).Wait()

    Write-Host "Connected to Chrome DevTools Protocol."

    # ============================================================
    # TEST 1: Login as kareem.ceo@facestone.com
    # ============================================================
    Write-Host "`n=== TEST 1: CEO LOGIN ==="
    Invoke-CDPNavigate -ws $ws -url "$BaseUrl"
    Start-Sleep -Seconds 1

    # Fill login form
    $evalFill = @"
(function() {
    document.getElementById('login-id-input').value = 'kareem.ceo@facestone.com';
    document.getElementById('login-pwd-input').value = 'Password123!';
    document.querySelector('#auth-login-form button[type="submit"]').click();
    return 'submitted';
})()
"@
    $res = Invoke-CDPEval -ws $ws -expression $evalFill
    Write-Host "Submitted login: $res"
    Start-Sleep -Seconds 2

    # Check DOM state on /departments
    $evalCheckCeo = @"
(function() {
    const cardFinance = document.getElementById('card-dept-finance');
    const cardTrans = document.getElementById('card-dept-transportation');
    const cardStorage = document.getElementById('card-dept-storage');
    const btnStorage = document.getElementById('btn-enter-storage');

    return {
        url: window.location.pathname,
        financeVisible: !!(cardFinance && window.getComputedStyle(cardFinance).display !== 'none'),
        transVisible: !!(cardTrans && window.getComputedStyle(cardTrans).display !== 'none'),
        storageVisible: !!(cardStorage && window.getComputedStyle(cardStorage).display !== 'none'),
        storageBtnVisible: !!(btnStorage && window.getComputedStyle(btnStorage).display !== 'none' && btnStorage.offsetParent !== null),
        storageBtnText: btnStorage ? btnStorage.innerText.trim() : null
    };
})()
"@
    $checkCeo = Invoke-CDPEval -ws $ws -expression $evalCheckCeo
    Write-Host ("CEO Department View: " + ($checkCeo | ConvertTo-Json -Compress))

    Invoke-CDPScreenshot -ws $ws -outputPath "F:\FaceStoneWebsite\scratch\test1_ceo_departments.png"
    Write-Host "Screenshot saved: test1_ceo_departments.png"

    # ============================================================
    # TEST 2: Click [ Enter Storage -> ]
    # ============================================================
    Write-Host "`n=== TEST 2: CLICK ENTER STORAGE ==="
    $evalClickStorage = @"
(function() {
    const btn = document.getElementById('btn-enter-storage');
    if (btn) { btn.click(); return true; }
    return false;
})()
"@
    $clicked = Invoke-CDPEval -ws $ws -expression $evalClickStorage
    Write-Host "Clicked Enter Storage: $clicked"
    Start-Sleep -Seconds 2

    # Check Storage page view
    $evalCheckStoragePage = @"
(function() {
    const storageRoot = document.getElementById('storage-page-root');
    const backBtn = document.getElementById('storage-back-to-departments-btn');
    const heroBackBtn = document.getElementById('storage-placeholder-back-btn');
    const heroTitle = document.querySelector('.storage-hero-title');
    const pill = document.querySelector('.storage-coming-soon-pill');

    return {
        url: window.location.pathname,
        storageRootVisible: !!(storageRoot && window.getComputedStyle(storageRoot).display !== 'none'),
        headerBackBtnVisible: !!(backBtn && window.getComputedStyle(backBtn).display !== 'none'),
        heroBackBtnVisible: !!(heroBackBtn && window.getComputedStyle(heroBackBtn).display !== 'none'),
        heroTitle: heroTitle ? heroTitle.innerText.trim() : null,
        pillText: pill ? pill.innerText.trim() : null,
        bodyBg: window.getComputedStyle(document.body).backgroundColor,
        storageRootBg: storageRoot ? window.getComputedStyle(storageRoot).backgroundColor : null
    };
})()
"@
    $checkStorage = Invoke-CDPEval -ws $ws -expression $evalCheckStoragePage
    Write-Host ("Storage Page Status: " + ($checkStorage | ConvertTo-Json -Compress))

    Invoke-CDPScreenshot -ws $ws -outputPath "F:\FaceStoneWebsite\scratch\test2_storage_coming_soon.png"
    Write-Host "Screenshot saved: test2_storage_coming_soon.png"

    # ============================================================
    # TEST 3: Click [ <- Back to Departments ]
    # ============================================================
    Write-Host "`n=== TEST 3: BACK TO DEPARTMENTS ==="
    $evalClickBack = @"
(function() {
    const backBtn = document.getElementById('storage-back-to-departments-btn');
    if (backBtn) { backBtn.click(); return true; }
    return false;
})()
"@
    $backClicked = Invoke-CDPEval -ws $ws -expression $evalClickBack
    Write-Host "Clicked Back to Departments: $backClicked"
    Start-Sleep -Seconds 1

    $evalCheckBack = @"
(function() {
    const cardStorage = document.getElementById('card-dept-storage');
    return {
        url: window.location.pathname,
        storageVisible: !!(cardStorage && window.getComputedStyle(cardStorage).display !== 'none')
    };
})()
"@
    $checkBack = Invoke-CDPEval -ws $ws -expression $evalCheckBack
    Write-Host ("Back to Departments Status: " + ($checkBack | ConvertTo-Json -Compress))

    # ============================================================
    # TEST 4: Logout and Login as storage@facestone.com
    # ============================================================
    Write-Host "`n=== TEST 4: STORAGE MANAGER LOGIN ==="
    $evalLogout = @"
(function() {
    const logoutBtn = document.getElementById('dept-logout-btn');
    if (logoutBtn) { logoutBtn.click(); return true; }
    return false;
})()
"@
    $logoutClicked = Invoke-CDPEval -ws $ws -expression $evalLogout
    Write-Host "Clicked Logout: $logoutClicked"
    Start-Sleep -Seconds 1

    # Login as storage@facestone.com
    $evalFillStorageMgr = @"
(function() {
    document.getElementById('login-id-input').value = 'storage@facestone.com';
    document.getElementById('login-pwd-input').value = 'Storage123!';
    document.querySelector('#auth-login-form button[type="submit"]').click();
    return 'submitted';
})()
"@
    $resMgr = Invoke-CDPEval -ws $ws -expression $evalFillStorageMgr
    Write-Host "Submitted Storage Manager login: $resMgr"
    Start-Sleep -Seconds 2

    # Check DOM state for Storage Manager
    $evalCheckStorageMgr = @"
(function() {
    const cardFinance = document.getElementById('card-dept-finance');
    const cardTrans = document.getElementById('card-dept-transportation');
    const cardStorage = document.getElementById('card-dept-storage');
    const btnStorage = document.getElementById('btn-enter-storage');

    return {
        url: window.location.pathname,
        financeVisible: !!(cardFinance && window.getComputedStyle(cardFinance).display !== 'none'),
        transVisible: !!(cardTrans && window.getComputedStyle(cardTrans).display !== 'none'),
        storageVisible: !!(cardStorage && window.getComputedStyle(cardStorage).display !== 'none'),
        storageBtnVisible: !!(btnStorage && window.getComputedStyle(btnStorage).display !== 'none' && btnStorage.offsetParent !== null),
        storageBtnText: btnStorage ? btnStorage.innerText.trim() : null
    };
})()
"@
    $checkStorageMgr = Invoke-CDPEval -ws $ws -expression $evalCheckStorageMgr
    Write-Host ("Storage Manager Department View: " + ($checkStorageMgr | ConvertTo-Json -Compress))

    Invoke-CDPScreenshot -ws $ws -outputPath "F:\FaceStoneWebsite\scratch\test4_storage_mgr_departments.png"
    Write-Host "Screenshot saved: test4_storage_mgr_departments.png"

    # Click Enter Storage for Storage Manager
    $null = Invoke-CDPEval -ws $ws -expression $evalClickStorage
    Start-Sleep -Seconds 1
    $checkStorageMgrPage = Invoke-CDPEval -ws $ws -expression $evalCheckStoragePage
    Write-Host ("Storage Manager Coming Soon Page: " + ($checkStorageMgrPage | ConvertTo-Json -Compress))

    # Direct navigation test to /departments/storage with direct page refresh
    Write-Host "`n=== TEST 5: DIRECT URL NAVIGATION /departments/storage ==="
    Invoke-CDPNavigate -ws $ws -url "$BaseUrl/departments/storage"
    Start-Sleep -Seconds 2
    $checkDirectNav = Invoke-CDPEval -ws $ws -expression $evalCheckStoragePage
    Write-Host ("Direct Navigation to /departments/storage: " + ($checkDirectNav | ConvertTo-Json -Compress))

    Invoke-CDPScreenshot -ws $ws -outputPath "F:\FaceStoneWebsite\scratch\test5_direct_storage_nav.png"
    Write-Host "Screenshot saved: test5_direct_storage_nav.png"

    $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", [System.Threading.CancellationToken]::None).Wait()

} finally {
    if ($chromeProc -and !$chromeProc.HasExited) {
        Stop-Process -Id $chromeProc.Id -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $tempProfile) {
        Remove-Item -Path $tempProfile -Recurse -Force -ErrorAction SilentlyContinue
    }
}
