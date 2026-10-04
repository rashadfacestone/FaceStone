param(
    [string]$BaseUrl = "http://127.0.0.1:5124"
)

$ErrorActionPreference = "Stop"

$tempProfile = Join-Path $env:TEMP "chrome_cdp_profile_$(Get-Random)"
$chromeArgs = @(
    "--headless=new",
    "--remote-debugging-port=9222",
    "--window-size=1440,1100",
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
    $tabs = Invoke-RestMethod -Uri "http://127.0.0.1:9222/json"
    $targetTab = $tabs | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    $wsUrl = $targetTab.webSocketDebuggerUrl

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([System.Uri]::new($wsUrl), [System.Threading.CancellationToken]::None).Wait()
    Write-Host "Connected to Chrome DevTools Protocol."

    # 1. Navigate to base URL
    Invoke-CDPNavigate -ws $ws -url "$BaseUrl"
    Start-Sleep -Seconds 2

    # 2. Login as transport@facestone.com
    Write-Host "--- 1. Logging in as Transportation User ---"
    $loginExpr = @'
    (() => {
        fillDemoAccount('transport@facestone.com', 'Password123!');
        const form = document.getElementById('auth-login-form');
        if (form) {
            form.dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }));
            return 'submitted';
        }
        return 'no_form';
    })()
'@
    $resLogin = Invoke-CDPEval -ws $ws -expression $loginExpr
    Write-Host "Login action: $resLogin"
    Start-Sleep -Seconds 3

    # If on departments page, navigate to transportation
    $navExpr = @'
    (() => {
        const btnTrans = document.getElementById('btn-enter-transportation');
        if (btnTrans) {
            btnTrans.click();
            return 'clicked_transportation';
        }
        return 'already_on_target';
    })()
'@
    $resNav = Invoke-CDPEval -ws $ws -expression $navExpr
    Write-Host "Navigation action: $resNav"
    Start-Sleep -Seconds 2

    # 3. Enter PO-400 to advance to the transportation form (Step 2)
    Write-Host "--- 2. Entering PO-400 to view full Transportation form ---"
    $poExpr = @'
    (() => {
        const poInput = document.getElementById('trans-po-entry-input');
        const contBtn = document.getElementById('trans-po-continue-btn');
        if (poInput && contBtn) {
            poInput.value = 'PO-400';
            poInput.dispatchEvent(new Event('input', { bubbles: true }));
            contBtn.click();
            return 'po_submitted';
        }
        return 'no_po_input';
    })()
'@
    $resPo = Invoke-CDPEval -ws $ws -expression $poExpr
    Write-Host "PO submit: $resPo"
    Start-Sleep -Seconds 2

    # 4. Check Formula Text
    Write-Host "--- 3. Checking Formula Text ---"
    $formulaExpr = @'
    (() => {
        const el = document.querySelector('.shipping-time-formula');
        if (!el) return { found: false, text: '' };
        const text = el.textContent.trim();
        return {
            found: true,
            text: text,
            hasExternalLoaded: text.includes('\u0627\u0644\u0642\u0627\u0626\u0645 \u0627\u0644\u062e\u0627\u0631\u062c\u064a'),
            hasExternalEmpty: text.includes('\u0627\u0644\u0641\u0627\u0631\u063a \u0627\u0644\u062e\u0627\u0631\u062c\u064a')
        };
    })()
'@
    $resFormula = Invoke-CDPEval -ws $ws -expression $formulaExpr
    Write-Host "Formula text: $($resFormula.text)"
    if ($resFormula.found -and $resFormula.hasExternalLoaded -and (-not $resFormula.hasExternalEmpty)) {
        Write-Host "[PASS] Formula correctly displays: External Loaded minus Internal Loaded!" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] Formula text validation failed!" -ForegroundColor Red
        exit 1
    }

    # ==============================================================
    # Scenario 1: Internal Loaded = 08:30, External Loaded = 13:45
    # Expected: 05:15
    # ==============================================================
    Write-Host "`n--- Scenario 1: Internal 08:30, External 13:45, External Empty 18:00 ---" -ForegroundColor Yellow
    $s1Expr = @'
    (() => {
        const intLoaded = document.getElementById('trans-wb-int-loaded-time');
        const extLoaded = document.getElementById('trans-wb-ext-loaded-time');
        const extEmpty = document.getElementById('trans-wb-ext-empty-time');
        
        intLoaded.value = '2026-10-04T08:30';
        intLoaded.dispatchEvent(new Event('input', { bubbles: true }));
        intLoaded.dispatchEvent(new Event('change', { bubbles: true }));
        
        extLoaded.value = '2026-10-04T13:45';
        extLoaded.dispatchEvent(new Event('input', { bubbles: true }));
        extLoaded.dispatchEvent(new Event('change', { bubbles: true }));

        extEmpty.value = '2026-10-04T18:00';
        extEmpty.dispatchEvent(new Event('input', { bubbles: true }));
        extEmpty.dispatchEvent(new Event('change', { bubbles: true }));
        
        const display = document.getElementById('trans-shipping-time-display');
        const bolDisplay = document.getElementById('trans-bol-shipping-time-display');
        const error = document.getElementById('trans-shipping-time-error');
        
        return {
            shippingTime: display ? display.textContent.trim() : null,
            bolShippingTime: bolDisplay ? bolDisplay.value.trim() : null,
            isErrorVisible: error ? (error.style.display !== 'none') : false
        };
    })()
'@
    $resS1 = Invoke-CDPEval -ws $ws -expression $s1Expr
    Write-Host "Scenario 1 Display: $($resS1.shippingTime), BOL: $($resS1.bolShippingTime), ErrorVisible: $($resS1.isErrorVisible)"
    if ($resS1.shippingTime -eq "05:15" -and $resS1.bolShippingTime -eq "05:15" -and (-not $resS1.isErrorVisible)) {
        Write-Host "[PASS] Scenario 1: Successfully calculated 05:15 without error!" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] Scenario 1 failed! Expected 05:15, got $($resS1.shippingTime)" -ForegroundColor Red
        exit 1
    }

    # ==============================================================
    # Scenario 2: Internal Loaded = 10:00, External Loaded = 10:00
    # Expected: 00:00
    # ==============================================================
    Write-Host "`n--- Scenario 2: Internal 10:00, External 10:00 ---" -ForegroundColor Yellow
    $s2Expr = @'
    (() => {
        const intLoaded = document.getElementById('trans-wb-int-loaded-time');
        const extLoaded = document.getElementById('trans-wb-ext-loaded-time');
        
        intLoaded.value = '2026-10-04T10:00';
        intLoaded.dispatchEvent(new Event('input', { bubbles: true }));
        intLoaded.dispatchEvent(new Event('change', { bubbles: true }));
        
        extLoaded.value = '2026-10-04T10:00';
        extLoaded.dispatchEvent(new Event('input', { bubbles: true }));
        extLoaded.dispatchEvent(new Event('change', { bubbles: true }));
        
        const display = document.getElementById('trans-shipping-time-display');
        const bolDisplay = document.getElementById('trans-bol-shipping-time-display');
        const error = document.getElementById('trans-shipping-time-error');
        
        return {
            shippingTime: display ? display.textContent.trim() : null,
            bolShippingTime: bolDisplay ? bolDisplay.value.trim() : null,
            isErrorVisible: error ? (error.style.display !== 'none') : false
        };
    })()
'@
    $resS2 = Invoke-CDPEval -ws $ws -expression $s2Expr
    Write-Host "Scenario 2 Display: $($resS2.shippingTime), BOL: $($resS2.bolShippingTime), ErrorVisible: $($resS2.isErrorVisible)"
    if ($resS2.shippingTime -eq "00:00" -and $resS2.bolShippingTime -eq "00:00" -and (-not $resS2.isErrorVisible)) {
        Write-Host "[PASS] Scenario 2: Successfully calculated 00:00 without error!" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] Scenario 2 failed! Expected 00:00, got $($resS2.shippingTime)" -ForegroundColor Red
        exit 1
    }

    # ==============================================================
    # Scenario 3: Internal Loaded = 14:00, External Loaded = 12:00
    # Expected: Timing error and validation message visible
    # ==============================================================
    Write-Host "`n--- Scenario 3: Internal 14:00, External 12:00 (Negative) ---" -ForegroundColor Yellow
    $s3Expr = @'
    (() => {
        const intLoaded = document.getElementById('trans-wb-int-loaded-time');
        const extLoaded = document.getElementById('trans-wb-ext-loaded-time');
        
        intLoaded.value = '2026-10-04T14:00';
        intLoaded.dispatchEvent(new Event('input', { bubbles: true }));
        intLoaded.dispatchEvent(new Event('change', { bubbles: true }));
        
        extLoaded.value = '2026-10-04T12:00';
        extLoaded.dispatchEvent(new Event('input', { bubbles: true }));
        extLoaded.dispatchEvent(new Event('change', { bubbles: true }));
        
        const display = document.getElementById('trans-shipping-time-display');
        const bolDisplay = document.getElementById('trans-bol-shipping-time-display');
        const error = document.getElementById('trans-shipping-time-error');
        
        return {
            shippingTime: display ? display.textContent.trim() : null,
            bolShippingTime: bolDisplay ? bolDisplay.value.trim() : null,
            isErrorVisible: error ? (error.style.display !== 'none') : false,
            errorMessage: error ? error.textContent.trim() : '',
            isTimingError: display ? display.textContent.includes('\u062e\u0637\u0623') : false
        };
    })()
'@
    $resS3 = Invoke-CDPEval -ws $ws -expression $s3Expr
    Write-Host "Scenario 3 Display: $($resS3.shippingTime), isTimingErr: $($resS3.isTimingError), ErrorVisible: $($resS3.isErrorVisible), Msg: $($resS3.errorMessage)"
    if ($resS3.isTimingError -and $resS3.isErrorVisible -and ($resS3.errorMessage.Length -gt 5)) {
        Write-Host "[PASS] Scenario 3: Correctly showed timing error and validation message!" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] Scenario 3 failed!" -ForegroundColor Red
        exit 1
    }

    # ==============================================================
    # Scenario 4: Missing timestamp -> Shipping time = '—', no error
    # ==============================================================
    Write-Host "`n--- Scenario 4: Missing External Loaded Timestamp ---" -ForegroundColor Yellow
    $s4Expr = @'
    (() => {
        const extLoaded = document.getElementById('trans-wb-ext-loaded-time');
        extLoaded.value = '';
        extLoaded.dispatchEvent(new Event('input', { bubbles: true }));
        extLoaded.dispatchEvent(new Event('change', { bubbles: true }));
        
        const display = document.getElementById('trans-shipping-time-display');
        const bolDisplay = document.getElementById('trans-bol-shipping-time-display');
        const error = document.getElementById('trans-shipping-time-error');
        
        return {
            shippingTime: display ? display.textContent.trim() : null,
            bolShippingTime: bolDisplay ? bolDisplay.value.trim() : null,
            isErrorVisible: error ? (error.style.display !== 'none') : false,
            isDash: display ? (display.textContent.trim() === '\u2014' || display.textContent.trim() === '-') : false
        };
    })()
'@
    $resS4 = Invoke-CDPEval -ws $ws -expression $s4Expr
    Write-Host "Scenario 4 Display: $($resS4.shippingTime), isDash: $($resS4.isDash), ErrorVisible: $($resS4.isErrorVisible)"
    if ($resS4.isDash -and (-not $resS4.isErrorVisible)) {
        Write-Host "[PASS] Scenario 4: Correctly reset to dash and error cleared when timestamp is missing!" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] Scenario 4 failed!" -ForegroundColor Red
        exit 1
    }

    # Reset back to valid 05:15 and capture screenshot
    $resetExpr = @'
    (() => {
        const intLoaded = document.getElementById('trans-wb-int-loaded-time');
        const extLoaded = document.getElementById('trans-wb-ext-loaded-time');
        intLoaded.value = '2026-10-04T08:30';
        intLoaded.dispatchEvent(new Event('input', { bubbles: true }));
        intLoaded.dispatchEvent(new Event('change', { bubbles: true }));
        extLoaded.value = '2026-10-04T13:45';
        extLoaded.dispatchEvent(new Event('input', { bubbles: true }));
        extLoaded.dispatchEvent(new Event('change', { bubbles: true }));
        document.getElementById('trans-shipping-time-box')?.scrollIntoView({ behavior: 'instant', block: 'center' });
    })()
'@
    $null = Invoke-CDPEval -ws $ws -expression $resetExpr
    Start-Sleep -Seconds 1
    Invoke-CDPScreenshot -ws $ws -outputPath "F:\FaceStoneWebsite\scratch\verified_shipping_time_ui.png"
    Write-Host "Screenshot saved: F:\FaceStoneWebsite\scratch\verified_shipping_time_ui.png"

    Write-Host "`n=================================================="
    Write-Host "ALL FRONTEND UI SCENARIOS VERIFIED AND PASSED!" -ForegroundColor Green
    Write-Host "=================================================="

    $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", [System.Threading.CancellationToken]::None).Wait()

} finally {
    if ($chromeProc -and !$chromeProc.HasExited) {
        Stop-Process -Id $chromeProc.Id -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $tempProfile) {
        Remove-Item -Path $tempProfile -Recurse -Force -ErrorAction SilentlyContinue
    }
}
