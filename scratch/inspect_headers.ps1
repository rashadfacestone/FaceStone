param(
    [string]$BaseUrl = "http://127.0.0.1:5124"
)
$ErrorActionPreference = "Stop"

Get-Process chrome -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like "*remote-debugging-port=9222*" } | Stop-Process -Force -ErrorAction SilentlyContinue

$tempProfile = Join-Path $env:TEMP "chrome_cdp_inspect_$(Get-Random)"
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
        params = @{ width = $w; height = $h; deviceScaleFactor = 1; mobile = $mob }
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

    # Desktop mode
    Set-CDPMetrics $ws 1440 900 $false
    Invoke-CDPNavigate $ws "$BaseUrl/"
    Start-Sleep -Seconds 2

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
    Start-Sleep -Seconds 4

    # 1. Department Selection Header
    $metrics = Invoke-CDPEval $ws @"
    (()=>{
        const header = document.querySelector('.dept-selection-header');
        const logo = document.querySelector('.dept-brand-logo-img');
        const hRect = header ? header.getBoundingClientRect() : null;
        const lRect = logo ? logo.getBoundingClientRect() : null;
        return {
            headerHeight: hRect ? hRect.height : 0,
            headerWidth: hRect ? hRect.width : 0,
            hasHScroll: document.documentElement.scrollWidth > window.innerWidth,
            logoHeight: lRect ? lRect.height : 0,
            logoWidth: lRect ? lRect.width : 0
        };
    })()
"@

    Write-Host ("`n=== 1. DEPARTMENT SELECTION HEADER (Desktop 1440px) ===") -ForegroundColor Yellow
    Write-Host ("Header Height: " + $metrics.headerHeight + " px (Target: 70-85px)")
    Write-Host ("Header Width: " + $metrics.headerWidth + " px (Full width: 100%)")
    Write-Host ("Logo Height: " + $metrics.logoHeight + " px (Target: ~55px max)")
    Write-Host ("Logo Width: " + $metrics.logoWidth + " px")
    Write-Host ("Horizontal Scroll: " + $metrics.hasHScroll)
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\verified_dept_header.png"

    # 2. Transportation Header
    Invoke-CDPEval $ws "document.getElementById('btn-enter-transportation')?.click();"
    Start-Sleep -Seconds 2
    $transMetrics = Invoke-CDPEval $ws @"
    (()=>{
        const header = document.querySelector('.trans-page-header');
        const logo = document.querySelector('.trans-page-header .dept-brand-logo-img');
        const hRect = header ? header.getBoundingClientRect() : null;
        const lRect = logo ? logo.getBoundingClientRect() : null;
        return {
            headerHeight: hRect ? hRect.height : 0,
            logoHeight: lRect ? lRect.height : 0,
            logoWidth: lRect ? lRect.width : 0,
            hasHScroll: document.documentElement.scrollWidth > window.innerWidth
        };
    })()
"@
    Write-Host ("`n=== 2. TRANSPORTATION HEADER (Desktop 1440px) ===") -ForegroundColor Yellow
    Write-Host ("Header Height: " + $transMetrics.headerHeight + " px")
    Write-Host ("Logo Height: " + $transMetrics.logoHeight + " px")
    Write-Host ("Logo Width: " + $transMetrics.logoWidth + " px")
    Write-Host ("Horizontal Scroll: " + $transMetrics.hasHScroll)
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\verified_trans_header.png"

    # Back
    Invoke-CDPEval $ws "document.getElementById('trans-back-to-departments-btn')?.click();"
    Start-Sleep -Seconds 1

    # 3. Storage Header
    Invoke-CDPEval $ws "window.ChatApp.enterDepartment('storage');"
    Start-Sleep -Seconds 2
    $storageMetrics = Invoke-CDPEval $ws @"
    (()=>{
        const header = document.querySelector('.storage-page-header');
        const logo = document.querySelector('.storage-page-header .dept-brand-logo-img');
        const hRect = header ? header.getBoundingClientRect() : null;
        const lRect = logo ? logo.getBoundingClientRect() : null;
        return {
            headerHeight: hRect ? hRect.height : 0,
            logoHeight: lRect ? lRect.height : 0,
            logoWidth: lRect ? lRect.width : 0
        };
    })()
"@
    Write-Host ("`n=== 3. STORAGE HEADER (Desktop 1440px) ===") -ForegroundColor Yellow
    Write-Host ("Header Height: " + $storageMetrics.headerHeight + " px")
    Write-Host ("Logo Height: " + $storageMetrics.logoHeight + " px")
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\verified_storage_header.png"

    # Back
    Invoke-CDPEval $ws "document.getElementById('storage-back-to-departments-btn')?.click();"
    Start-Sleep -Seconds 1

    # 4. Management Header
    Invoke-CDPEval $ws "document.getElementById('btn-enter-management')?.click();"
    Start-Sleep -Seconds 2
    $mgmtMetrics = Invoke-CDPEval $ws @"
    (()=>{
        const header = document.querySelector('.mgmt-page-header');
        const logo = document.querySelector('.mgmt-page-header .dept-brand-logo-img');
        const hRect = header ? header.getBoundingClientRect() : null;
        const lRect = logo ? logo.getBoundingClientRect() : null;
        return {
            headerHeight: hRect ? hRect.height : 0,
            logoHeight: lRect ? lRect.height : 0,
            logoWidth: lRect ? lRect.width : 0
        };
    })()
"@
    Write-Host ("`n=== 4. MANAGEMENT HEADER (Desktop 1440px) ===") -ForegroundColor Yellow
    Write-Host ("Header Height: " + $mgmtMetrics.headerHeight + " px")
    Write-Host ("Logo Height: " + $mgmtMetrics.logoHeight + " px")
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\verified_mgmt_header.png"

    # Back
    Invoke-CDPEval $ws "document.getElementById('management-back-to-departments-btn')?.click();"
    Start-Sleep -Seconds 1

    # 5. Finance Hub Header
    Invoke-CDPEval $ws "document.getElementById('btn-enter-finance')?.click();"
    Start-Sleep -Seconds 2
    $finMetrics = Invoke-CDPEval $ws @"
    (()=>{
        const header = document.querySelector('.finance-hub-header');
        const logo = document.querySelector('.finance-hub-header .dept-brand-logo-img');
        const hRect = header ? header.getBoundingClientRect() : null;
        const lRect = logo ? logo.getBoundingClientRect() : null;
        return {
            headerHeight: hRect ? hRect.height : 0,
            logoHeight: lRect ? lRect.height : 0,
            logoWidth: lRect ? lRect.width : 0
        };
    })()
"@
    Write-Host ("`n=== 5. FINANCE HUB HEADER (Desktop 1440px) ===") -ForegroundColor Yellow
    Write-Host ("Header Height: " + $finMetrics.headerHeight + " px")
    Write-Host ("Logo Height: " + $finMetrics.logoHeight + " px")
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\verified_finance_header.png"

    # Back to Departments
    Invoke-CDPEval $ws "document.getElementById('finance-hub-back-to-departments-btn')?.click();"
    Start-Sleep -Seconds 1

    # 6. Mobile Test (390x844)
    Set-CDPMetrics $ws 390 844 $true
    Start-Sleep -Seconds 1
    $mobDeptMetrics = Invoke-CDPEval $ws @"
    (()=>{
        const header = document.querySelector('.dept-selection-header');
        const logo = document.querySelector('.dept-brand-logo-img');
        const hRect = header ? header.getBoundingClientRect() : null;
        const lRect = logo ? logo.getBoundingClientRect() : null;
        return {
            headerHeight: hRect ? hRect.height : 0,
            logoHeight: lRect ? lRect.height : 0,
            hasHScroll: document.documentElement.scrollWidth > window.innerWidth
        };
    })()
"@
    Write-Host ("`n=== 6. MOBILE HEADER (Mobile 390px) ===") -ForegroundColor Yellow
    Write-Host ("Mobile Header Height: " + $mobDeptMetrics.headerHeight + " px")
    Write-Host ("Mobile Logo Height: " + $mobDeptMetrics.logoHeight + " px")
    Write-Host ("Mobile Horizontal Scroll: " + $mobDeptMetrics.hasHScroll)
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\verified_mobile_header.png"

    # 7. Mobile Transportation Header (390x844)
    Invoke-CDPEval $ws "document.getElementById('btn-enter-transportation')?.click();"
    Start-Sleep -Seconds 2
    $mobTransMetrics = Invoke-CDPEval $ws @"
    (()=>{
        const header = document.querySelector('.trans-page-header');
        const logo = document.querySelector('.trans-page-header .dept-brand-logo-img');
        const hRect = header ? header.getBoundingClientRect() : null;
        const lRect = logo ? logo.getBoundingClientRect() : null;
        return {
            headerHeight: hRect ? hRect.height : 0,
            logoHeight: lRect ? lRect.height : 0,
            hasHScroll: document.documentElement.scrollWidth > window.innerWidth
        };
    })()
"@
    Write-Host ("`n=== 7. MOBILE TRANSPORTATION HEADER (Mobile 390px) ===") -ForegroundColor Yellow
    Write-Host ("Mobile Trans Header Height: " + $mobTransMetrics.headerHeight + " px")
    Write-Host ("Mobile Trans Logo Height: " + $mobTransMetrics.logoHeight + " px")
    Write-Host ("Mobile Trans Horizontal Scroll: " + $mobTransMetrics.hasHScroll)
    Invoke-CDPScreenshot $ws "f:\FaceStoneWebsite\scratch\verified_mobile_trans_header.png"

}
finally {
    if ($ws -and $ws.State -eq 'Open') { $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "done", [System.Threading.CancellationToken]::None).Wait() }
    if ($chromeProc -and !$chromeProc.HasExited) { Stop-Process -Id $chromeProc.Id -Force }
}
