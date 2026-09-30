param(
    [string]$BaseUrl = "http://127.0.0.1:5124"
)

$ErrorActionPreference = "Stop"

# 1. Start Chrome with remote debugging
$tempProfile = Join-Path $env:TEMP "chrome_cdp_scroll_$(Get-Random)"
$chromeArgs = @(
    "--headless=new",
    "--remote-debugging-port=9223",
    "--window-size=1280,800",
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
    $tabs = Invoke-RestMethod -Uri "http://127.0.0.1:9223/json"
    $targetTab = $tabs | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    $wsUrl = $targetTab.webSocketDebuggerUrl

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([System.Uri]::new($wsUrl), [System.Threading.CancellationToken]::None).Wait()

    Write-Host "Connected to Chrome DevTools Protocol."

    # Login as CEO
    Write-Host "`n=== LOGIN AS CEO ==="
    Invoke-CDPNavigate -ws $ws -url "$BaseUrl"
    Start-Sleep -Seconds 1

    $evalFill = @"
(function() {
    document.getElementById('login-id-input').value = 'kareem.ceo@facestone.com';
    document.getElementById('login-pwd-input').value = 'Password123!';
    document.querySelector('#auth-login-form button[type="submit"]').click();
    return 'login submitted';
})()
"@
    $res = Invoke-CDPEval -ws $ws -expression $evalFill
    Write-Host "Login: $res"
    Start-Sleep -Seconds 2

    # ============================================================
    # TEST 1: Transportation Department Scrolling
    # ============================================================
    Write-Host "`n=== TEST 1: TRANSPORTATION DEPARTMENT SCROLLING ==="
    $openTrans = @"
(function() {
    const btn = document.getElementById('btn-enter-transportation');
    if (btn) { btn.click(); return 'clicked trans btn'; }
    window.location.hash = '#/transportation';
    return 'navigated by hash';
})()
"@
    $navRes = Invoke-CDPEval -ws $ws -expression $openTrans
    Write-Host "Navigated: $navRes"
    Start-Sleep -Seconds 1

    # Check heights and initial scroll position
    $evalTransInit = @"
(function() {
    const docH = document.documentElement.scrollHeight;
    const winH = window.innerHeight;
    const bodyH = document.body.scrollHeight;
    const scrollY = window.scrollY;
    const htmlOverflowY = window.getComputedStyle(document.documentElement).overflowY;
    const bodyOverflowY = window.getComputedStyle(document.body).overflowY;
    return {
        docHeight: docH,
        windowHeight: winH,
        bodyHeight: bodyH,
        initialScrollY: scrollY,
        isScrollable: docH > winH,
        htmlOverflowY: htmlOverflowY,
        bodyOverflowY: bodyOverflowY
    };
})()
"@
    $transInit = Invoke-CDPEval -ws $ws -expression $evalTransInit
    Write-Host ("Doc Height: {0}px, Window Height: {1}px, IsScrollable: {2}" -f $transInit.docHeight, $transInit.windowHeight, $transInit.isScrollable)
    Write-Host ("html overflow-y: {0}, body overflow-y: {1}" -f $transInit.htmlOverflowY, $transInit.bodyOverflowY)

    # Scroll down 600px using window.scrollBy
    $scrollDownRes = Invoke-CDPEval -ws $ws -expression @"
(function() {
    window.scrollBy(0, 600);
    return window.scrollY;
})()
"@
    Write-Host "After scrollBy(0, 600) -> window.scrollY: $scrollDownRes"

    # Scroll to the very bottom
    $scrollToBottom = @"
(function() {
    window.scrollTo(0, document.documentElement.scrollHeight);
    const scrollY = window.scrollY;
    const submitBtn = document.getElementById('trans-submit-btn');
    const clearBtn = document.getElementById('trans-clear-btn');
    const recentTable = document.getElementById('trans-recent-table');
    const header = document.querySelector('.trans-page-header');
    
    const submitRect = submitBtn ? submitBtn.getBoundingClientRect() : null;
    const headerRect = header ? header.getBoundingClientRect() : null;
    
    return {
        bottomScrollY: scrollY,
        reachedBottom: scrollY > 0 && (scrollY + window.innerHeight >= document.documentElement.scrollHeight - 50),
        submitBtnVisible: submitRect && submitRect.top < window.innerHeight && submitRect.bottom > 0,
        headerStickyAtTop: headerRect && headerRect.top === 0
    };
})()
"@
    $bottomRes = Invoke-CDPEval -ws $ws -expression $scrollToBottom
    Write-Host ("Bottom scrollY: {0}, Reached Bottom: {1}, Submit Button in Viewport: {2}, Header Sticky at Top: {3}" -f $bottomRes.bottomScrollY, $bottomRes.reachedBottom, $bottomRes.submitBtnVisible, $bottomRes.headerStickyAtTop)

    # Scroll back to top
    $scrollToTop = @"
(function() {
    window.scrollTo(0, 0);
    return window.scrollY;
})()
"@
    $topRes = Invoke-CDPEval -ws $ws -expression $scrollToTop
    Write-Host "After scrollTo(0, 0) -> window.scrollY: $topRes"

    # ============================================================
    # TEST 2: Management Department Scrolling
    # ============================================================
    Write-Host "`n=== TEST 2: MANAGEMENT DEPARTMENT SCROLLING ==="
    # Go back to departments, then enter Management
    $openMgmt = @"
(function() {
    window.location.hash = '#/management';
    return 'navigated to management';
})()
"@
    $mgmtNavRes = Invoke-CDPEval -ws $ws -expression $openMgmt
    Write-Host "Navigated: $mgmtNavRes"
    Start-Sleep -Seconds 2

    # Check heights and initial scroll position
    $evalMgmtInit = @"
(function() {
    const docH = document.documentElement.scrollHeight;
    const winH = window.innerHeight;
    const scrollY = window.scrollY;
    return {
        docHeight: docH,
        windowHeight: winH,
        initialScrollY: scrollY,
        isScrollable: docH > winH
    };
})()
"@
    $mgmtInit = Invoke-CDPEval -ws $ws -expression $evalMgmtInit
    Write-Host ("Doc Height: {0}px, Window Height: {1}px, IsScrollable: {2}" -f $mgmtInit.docHeight, $mgmtInit.windowHeight, $mgmtInit.isScrollable)

    # Scroll down 600px
    $mgmtScrollDown = Invoke-CDPEval -ws $ws -expression @"
(function() {
    window.scrollBy(0, 600);
    return window.scrollY;
})()
"@
    Write-Host "After scrollBy(0, 600) -> window.scrollY: $mgmtScrollDown"

    # Scroll to bottom
    $mgmtScrollBottom = @"
(function() {
    window.scrollTo(0, document.documentElement.scrollHeight);
    const scrollY = window.scrollY;
    const table = document.getElementById('mgmt-data-table');
    const pagination = document.getElementById('mgmt-pagination-bar');
    const header = document.querySelector('.mgmt-page-header');
    
    const tableRect = table ? table.getBoundingClientRect() : null;
    const pagRect = pagination ? pagination.getBoundingClientRect() : null;
    const headerRect = header ? header.getBoundingClientRect() : null;
    
    return {
        bottomScrollY: scrollY,
        reachedBottom: scrollY > 0 && (scrollY + window.innerHeight >= document.documentElement.scrollHeight - 50),
        paginationInViewport: pagRect && pagRect.top < window.innerHeight,
        headerStickyAtTop: headerRect && headerRect.top === 0
    };
})()
"@
    $mgmtBottomRes = Invoke-CDPEval -ws $ws -expression $mgmtScrollBottom
    Write-Host ("Bottom scrollY: {0}, Reached Bottom: {1}, Pagination in Viewport: {2}, Header Sticky at Top: {3}" -f $mgmtBottomRes.bottomScrollY, $mgmtBottomRes.reachedBottom, $mgmtBottomRes.paginationInViewport, $mgmtBottomRes.headerStickyAtTop)

    # Scroll back to top
    $mgmtScrollTop = Invoke-CDPEval -ws $ws -expression @"
(function() {
    window.scrollTo(0, 0);
    return window.scrollY;
})()
"@
    Write-Host "After scrollTo(0, 0) -> window.scrollY: $mgmtScrollTop"

    # ============================================================
    # TEST 3: Finance Department (Verify still locked, no body scroll)
    # ============================================================
    Write-Host "`n=== TEST 3: FINANCE DEPARTMENT INTEGRITY ==="
    $openFinance = @"
(function() {
    window.location.hash = '#/finance';
    return 'navigated to finance';
})()
"@
    $finNavRes = Invoke-CDPEval -ws $ws -expression $openFinance
    Write-Host "Navigated: $finNavRes"
    Start-Sleep -Seconds 1

    $evalFin = @"
(function() {
    const docH = document.documentElement.scrollHeight;
    const winH = window.innerHeight;
    const chatRoot = document.getElementById('chat-app-root');
    const chatOverflow = window.getComputedStyle(chatRoot).overflow;
    return {
        docHeight: docH,
        windowHeight: winH,
        noBodyScroll: docH <= winH,
        chatRootOverflow: chatOverflow
    };
})()
"@
    $finRes = Invoke-CDPEval -ws $ws -expression $evalFin
    Write-Host ("Doc Height: {0}px, Window Height: {1}px, No Body Scroll: {2}, Chat Overflow: {3}" -f $finRes.docHeight, $finRes.windowHeight, $finRes.noBodyScroll, $finRes.chatRootOverflow)

    Write-Host "`n=== ALL TESTS COMPLETED SUCCESSFULLY ==="
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
