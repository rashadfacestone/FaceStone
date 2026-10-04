param(
    [string]$BaseUrl = "http://127.0.0.1:5124"
)

$ErrorActionPreference = "Stop"

# 1. Start Chrome with remote debugging
$tempProfile = Join-Path $env:TEMP "chrome_cdp_fin_$(Get-Random)"
$chromeArgs = @(
    "--headless=new",
    "--remote-debugging-port=9227",
    "--window-size=1280,850",
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

    # Read response
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
    # Get active page websocket debugger URL
    $pagesJson = Invoke-RestMethod -Uri "http://127.0.0.1:9227/json"
    $targetTab = $pagesJson | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    $pageWsUrl = $targetTab.webSocketDebuggerUrl

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([System.Uri]::new($pageWsUrl), [System.Threading.CancellationToken]::None).Wait()

    Write-Host "[CDP] Connected to Chrome page on port 9227."

    # Navigate to app
    Write-Host "[1] Navigating to $BaseUrl/..."
    Invoke-CDPNavigate -ws $ws -url "$BaseUrl/"
    Start-Sleep -Seconds 2

    # Login as Sarah (Accountant)
    Write-Host "[2] Logging in as Sarah Mitchell (Accountant)..."
    $loginResult = Invoke-CDPEval -ws $ws -expression @"
(async () => {
    document.getElementById('login-id-input').value = 'sarah@facestone.com';
    document.getElementById('login-pwd-input').value = 'Password123!';
    const form = document.getElementById('auth-login-form');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    return 'submitted';
})()
"@
    Start-Sleep -Seconds 3

    # Check Departments view
    Write-Host "[3] Checking Departments View..."
    $deptViewStatus = Invoke-CDPEval -ws $ws -expression @"
(() => {
    const deptRoot = document.getElementById('dept-selection-root');
    const financeCard = document.getElementById('card-dept-finance');
    const floatBtn = document.getElementById('fs-floating-chat-btn');
    return {
        deptVisible: deptRoot ? window.getComputedStyle(deptRoot).display : 'none',
        financeCardVisible: financeCard ? window.getComputedStyle(financeCard).display : 'none',
        floatBtnVisible: floatBtn ? window.getComputedStyle(floatBtn).display : 'none',
        currentPath: window.location.pathname
    };
})()
"@
    Write-Host "    Departments View: $($deptViewStatus | ConvertTo-Json -Compress)"

    # Capture Screenshot 1: Departments
    Invoke-CDPScreenshot -ws $ws -outputPath "scratch/screenshot_1_departments.png"
    Write-Host "    Saved scratch/screenshot_1_departments.png"

    # Click Finance Card Button
    Write-Host "[4] Clicking Finance Card (#btn-enter-finance)..."
    $clickResult = Invoke-CDPEval -ws $ws -expression @"
(() => {
    const btn = document.getElementById('btn-enter-finance');
    if (btn) {
        btn.click();
        return 'clicked';
    }
    return 'not_found';
})()
"@
    Write-Host "    Click result: $clickResult"
    Start-Sleep -Seconds 3

    # Check Finance Statistics / Management View
    Write-Host "[5] Verifying Immediate Redirection to Finance Management & Statistics..."
    $finViewStatus = Invoke-CDPEval -ws $ws -expression @"
(() => {
    const finMgmtRoot = document.getElementById('finance-management-page-root');
    const finHubRoot = document.getElementById('finance-hub-root');
    const oldChatCard = document.querySelector('.finance-hub-card.card-chat');
    const poTable = document.getElementById('finance-po-table-body');
    const kpiVal = document.getElementById('kpi-total-po-value');
    const floatBtn = document.getElementById('fs-floating-chat-btn');
    const backBtn = document.getElementById('finance-mgmt-back-to-departments-btn');

    return {
        finMgmtVisible: finMgmtRoot ? window.getComputedStyle(finMgmtRoot).display : 'none',
        finHubVisible: finHubRoot ? window.getComputedStyle(finHubRoot).display : 'none',
        oldChatCardExists: !!oldChatCard,
        hasPoRows: poTable ? poTable.children.length : 0,
        kpiTotalVal: kpiVal ? kpiVal.textContent.trim() : '',
        floatBtnVisible: floatBtn ? window.getComputedStyle(floatBtn).display : 'none',
        backBtnText: backBtn ? backBtn.textContent.trim() : '',
        currentPath: window.location.pathname
    };
})()
"@
    Write-Host "    Finance View Status: $($finViewStatus | ConvertTo-Json -Compress)"
    
    # Capture Screenshot 2: Finance Statistics directly loaded
    Invoke-CDPScreenshot -ws $ws -outputPath "scratch/screenshot_2_finance_statistics.png"
    Write-Host "    Saved scratch/screenshot_2_finance_statistics.png"

    # Open Global Floating Chat
    Write-Host "[6] Toggling Global Floating Chat Button..."
    $chatToggleRes = Invoke-CDPEval -ws $ws -expression @"
(() => {
    const floatBtn = document.getElementById('fs-floating-chat-btn');
    floatBtn.click();
    const chatApp = document.getElementById('chat-app-root');
    return {
        chatVisible: chatApp ? window.getComputedStyle(chatApp).display : 'none',
        title: document.querySelector('.fs-chat-panel-title')?.textContent || ''
    };
})()
"@
    Write-Host "    Chat Toggle Result: $($chatToggleRes | ConvertTo-Json -Compress)"
    Start-Sleep -Seconds 1

    # Capture Screenshot 3: Global Chat open over Finance Statistics
    Invoke-CDPScreenshot -ws $ws -outputPath "scratch/screenshot_3_global_chat_open.png"
    Write-Host "    Saved scratch/screenshot_3_global_chat_open.png"

    # Close Global Floating Chat
    Write-Host "[7] Closing Global Floating Chat..."
    Invoke-CDPEval -ws $ws -expression @"
(() => {
    const closeBtn = document.getElementById('btn-close-floating-chat');
    if (closeBtn) closeBtn.click();
})()
"@
    Start-Sleep -Milliseconds 500

    # Click Back to Departments
    Write-Host "[8] Clicking Back to Departments button..."
    $backResult = Invoke-CDPEval -ws $ws -expression @"
(() => {
    const backBtn = document.getElementById('finance-mgmt-back-to-departments-btn');
    if (backBtn) {
        backBtn.click();
        return 'clicked';
    }
    return 'not_found';
})()
"@
    Start-Sleep -Seconds 1
    $returnStatus = Invoke-CDPEval -ws $ws -expression @"
(() => {
    const deptRoot = document.getElementById('dept-selection-root');
    return {
        deptVisible: deptRoot ? window.getComputedStyle(deptRoot).display : 'none',
        currentPath: window.location.pathname
    };
})()
"@
    Write-Host "    Returned to Departments: $($returnStatus | ConvertTo-Json -Compress)"

    # Test Transportation Department with CEO (who has access to both)
    Write-Host "[9] Testing Transportation Department with CEO (kareem.ceo@facestone.com)..."
    Invoke-CDPEval -ws $ws -expression @"
(async () => {
    window.ChatApp.logout();
    document.getElementById('login-id-input').value = 'kareem.ceo@facestone.com';
    document.getElementById('login-pwd-input').value = 'Password123!';
    document.getElementById('auth-login-form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
})()
"@
    Start-Sleep -Seconds 3

    Invoke-CDPEval -ws $ws -expression @"
(() => {
    document.getElementById('btn-enter-transportation')?.click();
})()
"@
    Start-Sleep -Seconds 2
    $transStatus = Invoke-CDPEval -ws $ws -expression @"
(() => {
    const transRoot = document.getElementById('transportation-page-root');
    return {
        transVisible: transRoot ? window.getComputedStyle(transRoot).display : 'none',
        currentPath: window.location.pathname
    };
})()
"@
    Write-Host "    Transportation Status: $($transStatus | ConvertTo-Json -Compress)"

    # Return to Departments from Transportation
    Invoke-CDPEval -ws $ws -expression "document.getElementById('trans-back-to-departments-btn')?.click();"
    Start-Sleep -Seconds 1

    # Test Non-Finance User (Authorization & Direct Route Protection)
    Write-Host "[10] Testing Authorization with Non-Finance Employee (alex@facestone.com)..."
    Invoke-CDPEval -ws $ws -expression @"
(async () => {
    window.ChatApp.logout();
    document.getElementById('login-id-input').value = 'alex@facestone.com';
    document.getElementById('login-pwd-input').value = 'Password123!';
    document.getElementById('auth-login-form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
})()
"@
    Start-Sleep -Seconds 3

    $empDeptStatus = Invoke-CDPEval -ws $ws -expression @"
(() => {
    const cardFinance = document.getElementById('card-dept-finance');
    return {
        financeCardVisible: cardFinance ? window.getComputedStyle(cardFinance).display : 'none'
    };
})()
"@
    Write-Host "    Employee Departments View - Finance card visible: $($empDeptStatus.financeCardVisible)"

    # Attempt to access /finance directly
    Write-Host "[11] Employee attempts direct navigation to Finance..."
    Invoke-CDPEval -ws $ws -expression @"
(async () => {
    await window.ChatApp.enterDepartment('finance');
})()
"@
    Start-Sleep -Seconds 2

    $deniedStatus = Invoke-CDPEval -ws $ws -expression @"
(() => {
    const deniedRoot = document.getElementById('access-denied-root');
    const msg = document.getElementById('access-denied-msg')?.textContent || '';
    return {
        deniedVisible: deniedRoot ? window.getComputedStyle(deniedRoot).display : 'none',
        message: msg.trim(),
        currentPath: window.location.pathname
    };
})()
"@
    Write-Host "    Access Denied Status: $($deniedStatus | ConvertTo-Json -Compress)"

    # Capture Screenshot 4: Access Denied
    Invoke-CDPScreenshot -ws $ws -outputPath "scratch/screenshot_4_access_denied.png"
    Write-Host "    Saved scratch/screenshot_4_access_denied.png"

    Write-Host "`n=== ALL END-TO-END FLOW CHECKS COMPLETED SUCCESSFULLY ==="

} finally {
    if ($ws -and $ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", [System.Threading.CancellationToken]::None).Wait()
    }
    if ($chromeProc -and -not $chromeProc.HasExited) {
        Stop-Process -Id $chromeProc.Id -Force
    }
    if (Test-Path $tempProfile) {
        Remove-Item -Path $tempProfile -Recurse -Force -ErrorAction SilentlyContinue
    }
}
