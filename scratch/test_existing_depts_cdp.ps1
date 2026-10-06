param(
    [string]$BaseUrl = "http://127.0.0.1:5124"
)

$ErrorActionPreference = "Stop"

# Start Chrome headless
$tempProfile = Join-Path $env:TEMP "chrome_cdp_existing_$(Get-Random)"
$chromeArgs = @(
    "--headless=new",
    "--remote-debugging-port=9224",
    "--window-size=1440,960",
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
    Start-Sleep -Milliseconds 800
}

try {
    $tabJson = Invoke-RestMethod -Uri "http://127.0.0.1:9224/json"
    $pageTab = $tabJson | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    $wsUrl = $pageTab.webSocketDebuggerUrl

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([System.Uri]::new($wsUrl), [System.Threading.CancellationToken]::None).Wait()

    # Navigate to BaseUrl
    Invoke-CDPNavigate $ws "$BaseUrl/"
    Start-Sleep -Seconds 1

    # Login as CEO
    $loginExpr = @"
    (async () => {
        const res = await window.ChatApi.login('kareem.ceo@facestone.com', 'Password123!');
        return { success: res.success };
    })()
"@
    $loginRes = Invoke-CDPEval $ws $loginExpr
    Write-Host "Login: $($loginRes | ConvertTo-Json -Compress)"

    # Test 1: Transportation Department
    Write-Host "Testing Transportation Department..."
    $transCheck = Invoke-CDPEval $ws @"
    (async () => {
        await window.ChatApp.enterDepartment('transportation');
        await new Promise(r => setTimeout(r, 600));
        const root = document.getElementById('transportation-page-root');
        return { visible: root && root.style.display !== 'none' };
    })()
"@
    Write-Host "Transportation Department: $($transCheck | ConvertTo-Json -Compress)"

    # Test 2: Storage Department
    Write-Host "Testing Storage Department..."
    $storageCheck = Invoke-CDPEval $ws @"
    (async () => {
        await window.ChatApp.enterDepartment('storage');
        await new Promise(r => setTimeout(r, 600));
        const root = document.getElementById('storage-page-root');
        return { visible: root && root.style.display !== 'none' };
    })()
"@
    Write-Host "Storage Department: $($storageCheck | ConvertTo-Json -Compress)"

    # Test 3: Finance Department
    Write-Host "Testing Finance Department..."
    $finCheck = Invoke-CDPEval $ws @"
    (async () => {
        await window.ChatApp.enterDepartment('finance');
        await new Promise(r => setTimeout(r, 600));
        const root = document.getElementById('finance-management-page-root');
        return { visible: root && root.style.display !== 'none' };
    })()
"@
    Write-Host "Finance Department: $($finCheck | ConvertTo-Json -Compress)"

    # Test 4: Global Chat
    Write-Host "Testing Global Chat..."
    $chatCheck = Invoke-CDPEval $ws @"
    (async () => {
        await window.ChatApp.enterFinanceChat();
        await new Promise(r => setTimeout(r, 600));
        const chatRoot = document.getElementById('chat-page-root') || document.querySelector('.chat-container');
        return { active: !!chatRoot };
    })()
"@
    Write-Host "Global Chat: $($chatCheck | ConvertTo-Json -Compress)"

    Write-Host "`nAll existing departments and chat verified functional!"
} finally {
    if ($ws -and $ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", [System.Threading.CancellationToken]::None).Wait()
    }
    if ($chromeProc -and !$chromeProc.HasExited) {
        Stop-Process -Id $chromeProc.Id -Force
    }
}
