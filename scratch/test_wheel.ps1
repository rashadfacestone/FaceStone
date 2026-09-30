param(
    [string]$BaseUrl = "http://127.0.0.1:5124"
)

$ErrorActionPreference = "Stop"

$tempProfile = Join-Path $env:TEMP "chrome_cdp_wheel_$(Get-Random)"
$chromeArgs = @(
    "--headless=new",
    "--remote-debugging-port=9224",
    "--window-size=1280,800",
    "--user-data-dir=$tempProfile",
    "about:blank"
)
$chromeProc = Start-Process -FilePath "C:\Program Files\Google\Chrome\Application\chrome.exe" -ArgumentList $chromeArgs -PassThru
Start-Sleep -Seconds 2

function Send-CDPCommand {
    param(
        [System.Net.WebSockets.ClientWebSocket]$ws,
        [string]$method,
        [hashtable]$params
    )
    $id = [System.Threading.Interlocked]::Increment([ref]$script:msgId)
    $payload = @{
        id = $id
        method = $method
        params = $params
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
    return ($json | ConvertFrom-Json)
}

function Invoke-CDPEval {
    param(
        [System.Net.WebSockets.ClientWebSocket]$ws,
        [string]$expression
    )
    $res = Send-CDPCommand -ws $ws -method "Runtime.evaluate" -params @{
        expression = $expression
        returnByValue = $true
        awaitPromise = $true
    }
    return $res.result.result.value
}

try {
    $script:msgId = 0
    $tabs = Invoke-RestMethod -Uri "http://127.0.0.1:9224/json"
    $targetTab = $tabs | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    $wsUrl = $targetTab.webSocketDebuggerUrl

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([System.Uri]::new($wsUrl), [System.Threading.CancellationToken]::None).Wait()

    # Login as CEO
    $null = Send-CDPCommand -ws $ws -method "Page.navigate" -params @{ url = "$BaseUrl" }
    Start-Sleep -Seconds 1

    $null = Invoke-CDPEval -ws $ws -expression @"
(function() {
    document.getElementById('login-id-input').value = 'kareem.ceo@facestone.com';
    document.getElementById('login-pwd-input').value = 'Password123!';
    document.querySelector('#auth-login-form button[type="submit"]').click();
})()
"@
    Start-Sleep -Seconds 2

    # 1. Test Wheel on Transportation
    Write-Host "=== TEST WHEEL ON TRANSPORTATION ==="
    $null = Invoke-CDPEval -ws $ws -expression "window.location.hash = '#/transportation';"
    Start-Sleep -Seconds 1

    $initialY = Invoke-CDPEval -ws $ws -expression "window.scrollY"
    Write-Host "Transportation initial window.scrollY: $initialY"

    # Dispatch mouse wheel event down (deltaY = 300)
    $null = Send-CDPCommand -ws $ws -method "Input.dispatchMouseEvent" -params @{
        type = "mouseWheel"
        x = 500
        y = 400
        deltaX = 0
        deltaY = 300
    }
    Start-Sleep -Milliseconds 500

    $afterWheelY = Invoke-CDPEval -ws $ws -expression "window.scrollY"
    Write-Host "Transportation after mouse wheel down (deltaY=300) -> window.scrollY: $afterWheelY"
    if ($afterWheelY -le $initialY) {
        throw "Mouse wheel failed to scroll down on Transportation Department!"
    }

    # Dispatch mouse wheel event back up (deltaY = -300)
    $null = Send-CDPCommand -ws $ws -method "Input.dispatchMouseEvent" -params @{
        type = "mouseWheel"
        x = 500
        y = 400
        deltaX = 0
        deltaY = -300
    }
    Start-Sleep -Milliseconds 500
    $afterWheelUpY = Invoke-CDPEval -ws $ws -expression "window.scrollY"
    Write-Host "Transportation after mouse wheel up (deltaY=-300) -> window.scrollY: $afterWheelUpY"

    # 2. Test Wheel on Management
    Write-Host "`n=== TEST WHEEL ON MANAGEMENT ==="
    $null = Invoke-CDPEval -ws $ws -expression "window.location.hash = '#/management';"
    Start-Sleep -Seconds 2

    $mgmtInitY = Invoke-CDPEval -ws $ws -expression "window.scrollY"
    Write-Host "Management initial window.scrollY: $mgmtInitY"

    # Dispatch mouse wheel event down (deltaY = 300)
    $null = Send-CDPCommand -ws $ws -method "Input.dispatchMouseEvent" -params @{
        type = "mouseWheel"
        x = 500
        y = 400
        deltaX = 0
        deltaY = 300
    }
    Start-Sleep -Milliseconds 500

    $mgmtAfterWheelY = Invoke-CDPEval -ws $ws -expression "window.scrollY"
    Write-Host "Management after mouse wheel down (deltaY=300) -> window.scrollY: $mgmtAfterWheelY"
    if ($mgmtAfterWheelY -le $mgmtInitY) {
        throw "Mouse wheel failed to scroll down on Management Department!"
    }

    # Dispatch mouse wheel event back up (deltaY = -300)
    $null = Send-CDPCommand -ws $ws -method "Input.dispatchMouseEvent" -params @{
        type = "mouseWheel"
        x = 500
        y = 400
        deltaX = 0
        deltaY = -300
    }
    Start-Sleep -Milliseconds 500
    $mgmtAfterWheelUpY = Invoke-CDPEval -ws $ws -expression "window.scrollY"
    Write-Host "Management after mouse wheel up (deltaY=-300) -> window.scrollY: $mgmtAfterWheelUpY"

    Write-Host "`n=== WHEEL SCROLLING VERIFIED ON BOTH PAGES ==="
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
