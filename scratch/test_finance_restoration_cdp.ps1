param(
    [string]$BaseUrl = "http://localhost:5124"
)

$ErrorActionPreference = "Stop"

# 1. Start application if not already running
$tcpConn = Get-NetTCPConnection -LocalPort 5124 -ErrorAction SilentlyContinue
$startedServer = $null
if (-not $tcpConn) {
    Write-Host "[TEST] Starting FaceStone.Web on port 5124..." -ForegroundColor Cyan
    $startedServer = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "FaceStone.Web.csproj", "--no-build" -PassThru -WindowStyle Hidden
    
    $healthy = $false
    for ($i = 0; $i -lt 25; $i++) {
        Start-Sleep -Seconds 1
        try {
            $h = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 2 -ErrorAction Stop
            if ($h.status -eq "Healthy") {
                $healthy = $true
                Write-Host "[TEST] Server is responsive and healthy." -ForegroundColor Green
                break
            }
        } catch {}
    }
    if (-not $healthy) {
        if ($startedServer) { Stop-Process -Id $startedServer.Id -Force -ErrorAction SilentlyContinue }
        throw "Server failed to start."
    }
} else {
    Write-Host "[TEST] Server already running on port 5124." -ForegroundColor Green
}

# 2. Chrome Headless with CDP
$chromePath = "C:\Program Files\Google\Chrome\Application\chrome.exe"
if (-not (Test-Path $chromePath)) {
    $chromePath = "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
}
$tempProfile = Join-Path $env:TEMP "chrome_cdp_fin_test_$(Get-Random)"
$chromeProc = Start-Process -FilePath $chromePath -ArgumentList @(
    "--headless=new",
    "--remote-debugging-port=9222",
    "--window-size=1440,1100",
    "--user-data-dir=$tempProfile",
    "http://127.0.0.1:5124"
) -PassThru

Start-Sleep -Seconds 3

try {
    $pages = Invoke-RestMethod -Uri "http://127.0.0.1:9222/json"
    $targetTab = $pages | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    $wsUrl = $targetTab.webSocketDebuggerUrl

    Write-Host "Connected to Chrome CDP: $wsUrl" -ForegroundColor Green

    $ws = New-Object System.Net.WebSockets.ClientWebSocket
    $cts = New-Object System.Threading.CancellationTokenSource
    $ws.ConnectAsync([Uri]$wsUrl, $cts.Token).GetAwaiter().GetResult()

    $global:cdpId = 1
    function Send-CDP ($method, $params = @{}) {
        $id = [System.Threading.Interlocked]::Increment([ref]$global:cdpId)
        $payload = @{
            id = $id
            method = $method
            params = $params
        } | ConvertTo-Json -Depth 10 -Compress
        
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
        $segment = New-Object System.ArraySegment[byte] -ArgumentList @($bytes, 0, $bytes.Length)
        $ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).GetAwaiter().GetResult()
        
        $buffer = New-Object byte[] 131072
        $ms = New-Object System.IO.MemoryStream
        do {
            $seg = New-Object System.ArraySegment[byte] -ArgumentList @($buffer, 0, $buffer.Length)
            $res = $ws.ReceiveAsync($seg, $cts.Token).GetAwaiter().GetResult()
            $ms.Write($buffer, 0, $res.Count)
        } while (!$res.EndOfMessage)
        $respJson = [System.Text.Encoding]::UTF8.GetString($ms.ToArray())
        return ConvertFrom-Json $respJson
    }

    function Evaluate-JS ($expr) {
        $res = Send-CDP "Runtime.evaluate" @{ expression = $expr; returnByValue = $true; awaitPromise = $true }
        return $res.result.result.value
    }

    function Capture-Screenshot ($filename) {
        $res = Send-CDP "Page.captureScreenshot" @{ format = "png" }
        $base64 = $res.result.data
        $bytes = [System.Convert]::FromBase64String($base64)
        [System.IO.File]::WriteAllBytes("f:\FaceStoneWebsite\scratch\$filename", $bytes)
        Write-Host "Screenshot saved: f:\FaceStoneWebsite\scratch\$filename" -ForegroundColor Yellow
    }

    Start-Sleep -Seconds 2

    # Login as Sarah Accountant
    Write-Host "`n[CDP] Logging in as Sarah Accountant..." -ForegroundColor Cyan
    $loginRes = Evaluate-JS @"
    (async () => {
        if (typeof fillDemoAccount === 'function') {
            fillDemoAccount('sarah@facestone.com', 'Password123!');
        } else {
            const idInput = document.getElementById('login-id-input');
            const pwdInput = document.getElementById('login-pwd-input');
            if (idInput) { idInput.value = 'sarah@facestone.com'; idInput.dispatchEvent(new Event('input', {bubbles: true})); }
            if (pwdInput) { pwdInput.value = 'Password123!'; pwdInput.dispatchEvent(new Event('input', {bubbles: true})); }
        }
        const form = document.getElementById('auth-login-form');
        if (form) {
            form.dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }));
            return 'LOGIN_SUBMITTED';
        }
        return 'FORM_NOT_FOUND';
    })()
"@
    Write-Host "Login Result: $loginRes"
    Start-Sleep -Seconds 3

    # Navigate to Finance
    Write-Host "`n[CDP] Entering Finance Department..." -ForegroundColor Cyan
    $enterRes = Evaluate-JS @"
    (async () => {
        const btn = document.getElementById('btn-enter-finance');
        if (btn) {
            btn.click();
            return 'CLICKED_BTN_ENTER_FINANCE';
        }
        if (window.ChatApp && typeof window.ChatApp.enterDepartment === 'function') {
            await window.ChatApp.enterDepartment('finance');
            return 'ENTERED_VIA_CHATAPP';
        }
        return 'BTN_NOT_FOUND';
    })()
"@
    Write-Host "Finance Navigation Result: $enterRes"
    Start-Sleep -Seconds 3

    # Initial state verification
    $initState = Evaluate-JS @"
    (() => {
        const root = document.getElementById('finance-management-page-root');
        const dateInput = document.getElementById('finance-filter-main-date');
        const poSelect = document.getElementById('finance-filter-main-po');
        const perfVal = document.getElementById('finance-perf-percentage');
        const dailyTrans = document.getElementById('finance-daily-trans-count');
        const poBadge = document.getElementById('finance-summary-po-number');
        const expectedQty = document.getElementById('finance-summary-expected-qty');
        const tonPrice = document.getElementById('finance-summary-ton-price');

        return {
            rootVisible: root ? root.style.display !== 'none' : false,
            dateValue: dateInput ? dateInput.value : null,
            poOptionsCount: poSelect ? poSelect.options.length : 0,
            perfPercentage: perfVal ? perfVal.textContent : null,
            dailyTrans: dailyTrans ? dailyTrans.textContent : null,
            poBadgeText: poBadge ? poBadge.textContent : null,
            expectedQtyText: expectedQty ? expectedQty.textContent : null,
            tonPriceText: tonPrice ? tonPrice.textContent : null
        };
    })()
"@

    Write-Host "`n=== Initial Finance State (Default Today) ===" -ForegroundColor Green
    Write-Host "Finance Page Visible: $($initState.rootVisible)"
    Write-Host "Date Filter Value:    $($initState.dateValue)"
    Write-Host "PO Options Count:     $($initState.poOptionsCount)"
    Write-Host "Performance %:        $($initState.perfPercentage)"
    Write-Host "Daily Trans Count:    $($initState.dailyTrans)"
    Write-Host "PO Summary Badge:     $($initState.poBadgeText)"
    Write-Host "Expected Qty:         $($initState.expectedQtyText)"
    Write-Host "Ton Price:            $($initState.tonPriceText)"

    # Capture top screenshot (Filters + Daily Activity)
    Capture-Screenshot "restored_finance_filters_and_daily.png"

    # Select PO-100
    Write-Host "`n[CDP] Selecting PO-100 in dropdown..." -ForegroundColor Cyan
    $selectRes = Evaluate-JS @"
    (async () => {
        const poSelect = document.getElementById('finance-filter-main-po');
        if (poSelect) {
            poSelect.value = 'PO-100';
            poSelect.dispatchEvent(new Event('change', { bubbles: true }));
            return 'PO_SELECTED';
        }
        return 'PO_SELECT_NOT_FOUND';
    })()
"@
    Write-Host "Select PO-100 Result: $selectRes"
    Start-Sleep -Seconds 2

    # Verify State after selecting PO-100
    $poState = Evaluate-JS @"
    (() => {
        const perfVal = document.getElementById('finance-perf-percentage');
        const poBadge = document.getElementById('finance-summary-po-number');
        const expectedQty = document.getElementById('finance-summary-expected-qty');
        const deliveredQty = document.getElementById('finance-summary-delivered-qty');
        const remainingQty = document.getElementById('finance-summary-remaining-qty');
        const wasteQty = document.getElementById('finance-summary-total-waste');
        const tonPrice = document.getElementById('finance-summary-ton-price');
        const movementsCount = document.getElementById('finance-summary-movements-count');

        return {
            perfPercentage: perfVal ? perfVal.textContent : null,
            poBadgeText: poBadge ? poBadge.textContent : null,
            expectedQtyText: expectedQty ? expectedQty.textContent : null,
            deliveredQtyText: deliveredQty ? deliveredQty.textContent : null,
            remainingQtyText: remainingQty ? remainingQty.textContent : null,
            wasteQtyText: wasteQty ? wasteQty.textContent : null,
            tonPriceText: tonPrice ? tonPrice.textContent : null,
            movementsCountText: movementsCount ? movementsCount.textContent : null
        };
    })()
"@

    Write-Host "`n=== State After Selecting PO-100 ===" -ForegroundColor Green
    Write-Host "Performance %:        $($poState.perfPercentage)"
    Write-Host "PO Badge:             $($poState.poBadgeText)"
    Write-Host "Expected Qty:         $($poState.expectedQtyText)"
    Write-Host "Delivered Qty:        $($poState.deliveredQtyText)"
    Write-Host "Remaining Qty:        $($poState.remainingQtyText)"
    Write-Host "Total Waste:          $($poState.wasteQtyText)"
    Write-Host "Ton Price:            $($poState.tonPriceText)"
    Write-Host "Movements Count:      $($poState.movementsCountText)"

    # Scroll down to capture PO Summary and Factory Performance
    Evaluate-JS "window.scrollBy(0, 450);"
    Start-Sleep -Milliseconds 800
    Capture-Screenshot "restored_finance_po_and_performance.png"

    # Scroll to bottom table
    Evaluate-JS "window.scrollBy(0, 700);"
    Start-Sleep -Milliseconds 800
    Capture-Screenshot "restored_finance_table.png"

    Write-Host "`n[SUCCESS] E2E CDP Verification Complete!" -ForegroundColor Green

} finally {
    if ($ws) {
        try {
            if ($ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
                $null = $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", [System.Threading.CancellationToken]::None).Wait(2000)
            }
            $ws.Dispose()
        } catch {}
    }
    if ($chromeProc) {
        Stop-Process -Id $chromeProc.Id -Force -ErrorAction SilentlyContinue
    }
    if ($startedServer) {
        Write-Host "Stopping temporary server process..." -ForegroundColor Cyan
        Stop-Process -Id $startedServer.Id -Force -ErrorAction SilentlyContinue
    }
}
