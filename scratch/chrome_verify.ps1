param(
    [string]$BaseUrl = "http://127.0.0.1:5124"
)

$ErrorActionPreference = "Stop"

# Start Chrome in headless mode
$tempProfile = Join-Path $env:TEMP "chrome_cdp_verify_$(Get-Random)"
$chromeArgs = @(
    "--headless=new",
    "--remote-debugging-port=9223",
    "--window-size=1280,800",
    "--user-data-dir=$tempProfile",
    "$BaseUrl/"
)
$chromeProc = Start-Process -FilePath "C:\Program Files\Google\Chrome\Application\chrome.exe" -ArgumentList $chromeArgs -PassThru
Start-Sleep -Seconds 3

try {
    # Get WebSocket Debugger URL
    $tabs = Invoke-RestMethod -Uri "http://127.0.0.1:9223/json"
    $pageTab = $tabs | Where-Object { $_.type -eq "page" } | Select-Object -First 1

    if (-not $pageTab) {
        Write-Error "Could not find Chrome page tab"
    }

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([System.Uri]::new($pageTab.webSocketDebuggerUrl), [System.Threading.CancellationToken]::None).Wait()

    $msgId = 0
    function Send-CDP([string]$method, [hashtable]$params) {
        $id = ++$script:msgId
        $dict = [ordered]@{ id = $id; method = $method; params = $params }
        $json = $dict | ConvertTo-Json -Compress -Depth 10
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
        $ws.SendAsync([System.ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()

        $buffer = [byte[]]::new(1048576)
        $ms = [System.IO.MemoryStream]::new()
        do {
            $seg = [System.ArraySegment[byte]]::new($buffer)
            $res = $ws.ReceiveAsync($seg, [System.Threading.CancellationToken]::None).Result
            $ms.Write($buffer, 0, $res.Count)
        } while (!$res.EndOfMessage)

        return ([System.Text.Encoding]::UTF8.GetString($ms.ToArray()) | ConvertFrom-Json)
    }

    # Enable Page and Runtime
    $null = Send-CDP "Page.enable" @{}
    $null = Send-CDP "Runtime.enable" @{}

    Start-Sleep -Seconds 2

    # Check Document Title
    $evalTitle = Send-CDP "Runtime.evaluate" @{ expression = "document.title"; returnByValue = $true }
    Write-Host "Real Chrome Document Title:" $evalTitle.result.result.value

    # Check Favicon href
    $evalFavicon = Send-CDP "Runtime.evaluate" @{ expression = 'document.querySelector("link[rel=''icon'']").getAttribute("href")'; returnByValue = $true }
    Write-Host "Real Chrome Favicon href:" $evalFavicon.result.result.value

    # Take Desktop Screenshot
    $shotDesktop = Send-CDP "Page.captureScreenshot" @{ format = "png" }
    [System.IO.File]::WriteAllBytes("f:\FaceStoneWebsite\scratch\chrome_desktop_verified.png", [System.Convert]::FromBase64String($shotDesktop.result.data))
    Write-Host "Captured desktop screenshot to scratch\chrome_desktop_verified.png"

    # Emulate Mobile View (390 x 844)
    $null = Send-CDP "Emulation.setDeviceMetricsOverride" @{
        width = 390
        height = 844
        deviceScaleFactor = 2
        mobile = $true
    }
    Start-Sleep -Seconds 1

    # Take Mobile Screenshot
    $shotMobile = Send-CDP "Page.captureScreenshot" @{ format = "png" }
    [System.IO.File]::WriteAllBytes("f:\FaceStoneWebsite\scratch\chrome_mobile_verified.png", [System.Convert]::FromBase64String($shotMobile.result.data))
    Write-Host "Captured mobile screenshot to scratch\chrome_mobile_verified.png"

    $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", [System.Threading.CancellationToken]::None).Wait()
}
finally {
    if ($chromeProc -and -not $chromeProc.HasExited) {
        $chromeProc.Kill()
    }
    Remove-Item $tempProfile -Recurse -Force -ErrorAction SilentlyContinue
}
