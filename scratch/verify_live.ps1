param([string]$BaseUrl = "http://127.0.0.1:5124")

$ErrorActionPreference = "Stop"

$tempProfile = Join-Path $env:TEMP "chrome_cdp_test_$(Get-Random)"
$chromeArgs = @(
    "--headless=new",
    "--remote-debugging-port=9222",
    "--window-size=1280,800",
    "--user-data-dir=$tempProfile",
    "about:blank"
)
$chromeProc = Start-Process -FilePath "C:\Program Files\Google\Chrome\Application\chrome.exe" -ArgumentList $chromeArgs -PassThru
Start-Sleep -Seconds 2

try {
    $tabs = Invoke-RestMethod -Uri "http://127.0.0.1:9222/json"
    $pageTab = $tabs | Where-Object { $_.type -eq "page" } | Select-Object -First 1

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([System.Uri]::new($pageTab.webSocketDebuggerUrl), [System.Threading.CancellationToken]::None).Wait()

    $script:msgId = 0
    function Send-CDP([string]$method, [hashtable]$params) {
        $id = ++$script:msgId
        $dict = [ordered]@{ id = $id; method = $method; params = $params }
        $json = $dict | ConvertTo-Json -Compress -Depth 10
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
        $ws.SendAsync([System.ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()

        $buffer = [byte[]]::new(2097152) # 2MB
        $ms = [System.IO.MemoryStream]::new()
        do {
            $seg = [System.ArraySegment[byte]]::new($buffer)
            $res = $ws.ReceiveAsync($seg, [System.Threading.CancellationToken]::None).Result
            $ms.Write($buffer, 0, $res.Count)
        } while (!$res.EndOfMessage)

        return ([System.Text.Encoding]::UTF8.GetString($ms.ToArray()) | ConvertFrom-Json)
    }

    $null = Send-CDP "Page.enable" @{}
    $null = Send-CDP "Runtime.enable" @{}

    # Navigate to site
    Write-Host "Navigating to $BaseUrl/ ..."
    $null = Send-CDP "Page.navigate" @{ url = "$BaseUrl/" }
    Start-Sleep -Seconds 3

    # Document title
    $evalTitle = Send-CDP "Runtime.evaluate" @{ expression = "document.title"; returnByValue = $true }
    Write-Host "Document Title in Chrome:" $evalTitle.result.result.value

    # Favicon href
    $evalFav = Send-CDP "Runtime.evaluate" @{ expression = 'document.querySelector("link[rel=''icon'']").getAttribute("href")'; returnByValue = $true }
    Write-Host "Favicon href in Chrome:" $evalFav.result.result.value

    # Check for console errors or unhandled exceptions
    $evalErrors = Send-CDP "Runtime.evaluate" @{ expression = "window.__errors || 'none'"; returnByValue = $true }
    Write-Host "Window errors:" $evalErrors.result.result.value

    # Capture Desktop Screenshot
    $shotDesktop = Send-CDP "Page.captureScreenshot" @{ format = "png" }
    [System.IO.File]::WriteAllBytes("f:\FaceStoneWebsite\scratch\chrome_desktop_live.png", [System.Convert]::FromBase64String($shotDesktop.result.data))
    Write-Host "Saved f:\FaceStoneWebsite\scratch\chrome_desktop_live.png"

    # Resize to Mobile (390 x 844)
    $null = Send-CDP "Emulation.setDeviceMetricsOverride" @{
        width = 390
        height = 844
        deviceScaleFactor = 2
        mobile = $true
    }
    Start-Sleep -Seconds 1

    # Capture Mobile Screenshot
    $shotMobile = Send-CDP "Page.captureScreenshot" @{ format = "png" }
    [System.IO.File]::WriteAllBytes("f:\FaceStoneWebsite\scratch\chrome_mobile_live.png", [System.Convert]::FromBase64String($shotMobile.result.data))
    Write-Host "Saved f:\FaceStoneWebsite\scratch\chrome_mobile_live.png"
}
finally {
    if ($ws) {
        try { $ws.Dispose() } catch {}
    }
    if ($chromeProc -and !$chromeProc.HasExited) {
        try { Stop-Process -Id $chromeProc.Id -Force } catch {}
    }
    Remove-Item $tempProfile -Recurse -Force -ErrorAction SilentlyContinue
}
