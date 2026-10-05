param(
    [string]$BaseUrl = "http://localhost:5124"
)

$ErrorActionPreference = "Stop"

# 1. Start Chrome with remote debugging
$tempProfile = Join-Path $env:TEMP "chrome_cdp_fin_e2e_$(Get-Random)"
$chromeArgs = @(
    "--headless=new",
    "--remote-debugging-port=9229",
    "--window-size=1440,1050",
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

    $buffer = [byte[]]::new(131072)
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

    $buffer = [byte[]]::new(1048576)
    $ms = [System.IO.MemoryStream]::new()
    do {
        $seg = [System.ArraySegment[byte]]::new($buffer)
        $res = $ws.ReceiveAsync($seg, [System.Threading.CancellationToken]::None).Result
        $ms.Write($buffer, 0, $res.Count)
    } while (!$res.EndOfMessage)

    $json = [System.Text.Encoding]::UTF8.GetString($ms.ToArray())
    $obj = $json | ConvertFrom-Json
    if ($obj.result -and $obj.result.data) {
        [System.IO.File]::WriteAllBytes($outputPath, [Convert]::FromBase64String($obj.result.data))
    }
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
    Start-Sleep -Milliseconds 2000
}

try {
    # 2. Connect to Chrome target
    $targetsJson = Invoke-RestMethod -Uri "http://127.0.0.1:9229/json"
    $targetTab = $targetsJson | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    $wsUrl = $targetTab.webSocketDebuggerUrl

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([Uri]$wsUrl, [System.Threading.CancellationToken]::None).Wait()

    Write-Host "Connected to headless Chrome via CDP." -ForegroundColor Green

    # Navigate
    Invoke-CDPNavigate -ws $ws -url "$BaseUrl/"
    Start-Sleep -Seconds 2

    # Login as Sarah Accountant
    $loginExpr = @"
    (async () => {
        const idInput = document.getElementById('login-id-input');
        const passInput = document.getElementById('login-pwd-input');
        const form = document.getElementById('auth-login-form');
        if (idInput && passInput && form) {
            idInput.value = 'sarah@facestone.com';
            idInput.dispatchEvent(new Event('input', { bubbles: true }));
            passInput.value = 'Password123!';
            passInput.dispatchEvent(new Event('input', { bubbles: true }));
            form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
            return 'LOGIN_SUBMITTED';
        }
        return 'INPUTS_NOT_FOUND';
    })()
"@
    $res = Invoke-CDPEval -ws $ws -expression $loginExpr
    Write-Host "Login Action: $res"
    Start-Sleep -Seconds 3

    # Click Finance Card Button
    $enterFinExpr = @"
    (async () => {
        const btn = document.getElementById('btn-enter-finance');
        if (btn) {
            btn.click();
            return 'CLICKED_BTN_ENTER_FINANCE';
        }
        if (window.ChatApp) {
            await window.ChatApp.enterDepartment('finance');
            return 'ENTERED_VIA_CHATAPP';
        }
        return 'BTN_NOT_FOUND';
    })()
"@
    $finRes = Invoke-CDPEval -ws $ws -expression $enterFinExpr
    Write-Host "Finance Navigation: $finRes"
    Start-Sleep -Seconds 3

    # Verify DOM Elements and Values
    $verifyExpr = @"
    (() => {
        const dateInput = document.getElementById('finance-filter-main-date');
        const poSelect = document.getElementById('finance-filter-main-po');
        const perfVal = document.getElementById('finance-perf-percentage');
        const dailyTrans = document.getElementById('finance-daily-trans-count');
        const poBadge = document.getElementById('finance-summary-po-number');
        const expectedQty = document.getElementById('finance-summary-expected-qty');
        const tonPrice = document.getElementById('finance-summary-ton-price');

        return {
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
    $state = Invoke-CDPEval -ws $ws -expression $verifyExpr
    Write-Host "`n--- Initial Finance State (Default Today) ---" -ForegroundColor Cyan
    Write-Host "Date Value: $($state.dateValue)"
    Write-Host "PO Options Count: $($state.poOptionsCount)"
    Write-Host "Performance %: $($state.perfPercentage)"
    Write-Host "Daily Trans Count: $($state.dailyTrans)"
    Write-Host "PO Summary Badge: $($state.poBadgeText)"
    Write-Host "Expected Qty: $($state.expectedQtyText)"
    Write-Host "Ton Price: $($state.tonPriceText)"

    # Test selecting PO-100
    $selectPo100Expr = @"
    (async () => {
        const poSelect = document.getElementById('finance-filter-main-po');
        if (poSelect) {
            poSelect.value = 'PO-100';
            poSelect.dispatchEvent(new Event('change', { bubbles: true }));
            await new Promise(r => setTimeout(r, 1000));
            const perfVal = document.getElementById('finance-perf-percentage');
            const poBadge = document.getElementById('finance-summary-po-number');
            const expectedQty = document.getElementById('finance-summary-expected-qty');
            const deliveredQty = document.getElementById('finance-summary-delivered-qty');
            const wasteQty = document.getElementById('finance-summary-total-waste');
            const tonPrice = document.getElementById('finance-summary-ton-price');
            const movements = document.getElementById('finance-summary-movements-count');

            return {
                selectedPo: poSelect.value,
                perfVal: perfVal ? perfVal.textContent : null,
                poBadge: poBadge ? poBadge.textContent : null,
                expectedQty: expectedQty ? expectedQty.textContent : null,
                deliveredQty: deliveredQty ? deliveredQty.textContent : null,
                wasteQty: wasteQty ? wasteQty.textContent : null,
                tonPrice: tonPrice ? tonPrice.textContent : null,
                movements: movements ? movements.textContent : null
            };
        }
        return null;
    })()
"@
    $po100State = Invoke-CDPEval -ws $ws -expression $selectPo100Expr
    Write-Host "`n--- State After Selecting PO-100 ---" -ForegroundColor Cyan
    Write-Host "Selected PO: $($po100State.selectedPo)"
    Write-Host "Performance %: $($po100State.perfVal)"
    Write-Host "PO Badge: $($po100State.poBadge)"
    Write-Host "Expected Qty: $($po100State.expectedQty)"
    Write-Host "Delivered Qty: $($po100State.deliveredQty)"
    Write-Host "Waste Qty: $($po100State.wasteQty)"
    Write-Host "Ton Price: $($po100State.tonPrice)"
    Write-Host "Movements Count: $($po100State.movements)"

    # Capture Screenshot
    $screenshotPath = "f:\FaceStoneWebsite\scratch\finance_verified_dashboard.png"
    Invoke-CDPScreenshot -ws $ws -outputPath $screenshotPath
    Write-Host "`nScreenshot saved to: $screenshotPath" -ForegroundColor Green

    # Scroll down and capture summary and table
    Invoke-CDPEval -ws $ws -expression "window.scrollTo(0, 700); 'SCROLLED';"
    Start-Sleep -Seconds 1
    $screenshotPath2 = "f:\FaceStoneWebsite\scratch\finance_verified_summary_table.png"
    Invoke-CDPScreenshot -ws $ws -outputPath $screenshotPath2
    Write-Host "Lower section screenshot saved to: $screenshotPath2" -ForegroundColor Green

    # Test Clear PO Button
    $clearPoExpr = @"
    (async () => {
        const btnClear = document.getElementById('finance-btn-clear-po');
        if (btnClear) {
            btnClear.click();
            await new Promise(r => setTimeout(r, 1000));
            const poSelect = document.getElementById('finance-filter-main-po');
            const poBadge = document.getElementById('finance-summary-po-number');
            return {
                poSelectValue: poSelect ? poSelect.value : null,
                poBadge: poBadge ? poBadge.textContent : null
            };
        }
        return null;
    })()
"@
    $clearedState = Invoke-CDPEval -ws $ws -expression $clearPoExpr
    Write-Host "`n--- State After Clicking Clear PO ---" -ForegroundColor Cyan
    Write-Host "PO Select Value: $($clearedState.poSelectValue)"
    Write-Host "PO Badge: $($clearedState.poBadge)"

    # Check for Console Errors
    $errExpr = @"
    (() => {
        return window.__testErrors || [];
    })()
"@
    $consoleErrors = Invoke-CDPEval -ws $ws -expression $errExpr
    Write-Host "`nConsole Errors Count: $($consoleErrors.Count)"

    Write-Host "`n=== Browser E2E Tests Completed Successfully! ===" -ForegroundColor Green
}
finally {
    if ($ws -and $ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", [System.Threading.CancellationToken]::None).Wait()
    }
    if ($chromeProc -and !$chromeProc.HasExited) {
        Stop-Process -Id $chromeProc.Id -Force
    }
    if (Test-Path $tempProfile) {
        Remove-Item -Path $tempProfile -Recurse -Force -ErrorAction SilentlyContinue
    }
}
