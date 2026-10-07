param([string]$BaseUrl = "http://localhost:5124")
$tempProfile = Join-Path $env:TEMP "chrome_cdp_shot_$(Get-Random)"
$chromeProc = Start-Process -FilePath "C:\Program Files\Google\Chrome\Application\chrome.exe" -ArgumentList @("--headless=new", "--remote-debugging-port=9288", "--window-size=1440,1100", "--user-data-dir=$tempProfile", "$BaseUrl") -PassThru
Start-Sleep -Seconds 2

$script:mid = 0
function Call-CDP($ws, $method, $params) {
    $script:mid = ($script:mid + 1)
    $curId = $script:mid
    $payload = @{ id = $curId; method = $method; params = $params } | ConvertTo-Json -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $ws.SendAsync([System.ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()
    while ($true) {
        $buffer = [byte[]]::new(262144)
        $ms = [System.IO.MemoryStream]::new()
        do {
            $seg = [System.ArraySegment[byte]]::new($buffer)
            $res = $ws.ReceiveAsync($seg, [System.Threading.CancellationToken]::None).Result
            $ms.Write($buffer, 0, $res.Count)
        } while (!$res.EndOfMessage)
        $txt = [System.Text.Encoding]::UTF8.GetString($ms.ToArray())
        $resp = $txt | ConvertFrom-Json
        if ($resp.id -eq $curId) { return $resp }
    }
}

try {
    $tabsJson = (New-Object System.Net.WebClient).DownloadString("http://localhost:9288/json")
    $tabs = $tabsJson | ConvertFrom-Json
    $pageTab = $tabs | Where-Object { $_.type -eq "page" } | Select-Object -First 1

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([uri]$pageTab.webSocketDebuggerUrl, [System.Threading.CancellationToken]::None).Wait()
    Call-CDP $ws "Page.enable" @{} | Out-Null

    # Login
    Call-CDP $ws "Runtime.evaluate" @{ expression = "window.ChatApi.login('control_monitoring', 'Password123!')"; awaitPromise = $true } | Out-Null
    Start-Sleep -Milliseconds 500

    # Navigate to req 34
    Call-CDP $ws "Page.navigate" @{ url = "$BaseUrl/control-monitoring/request/transportation/34" } | Out-Null
    Start-Sleep -Seconds 3

    # Switch to details view for req 34
    Call-CDP $ws "Runtime.evaluate" @{ expression = @"
(async () => {
    if (window.ControlMonitoringApp) {
        window.ControlMonitoringApp.init();
        await window.ControlMonitoringApp.showRequestDetails('transportation', 34, false);
    }
    const root = document.getElementById('control-monitoring-page-root');
    if (root) root.style.display = 'block';
    const mainView = document.getElementById('control-monitoring-main-view');
    if (mainView) mainView.style.display = 'none';
    const detView = document.getElementById('control-monitoring-request-details-view');
    if (detView) detView.style.display = 'block';
})()
"@; awaitPromise = $true } | Out-Null
    Start-Sleep -Seconds 1

    # Scroll down to attachments section
    Call-CDP $ws "Runtime.evaluate" @{ expression = "document.getElementById('cm-details-attachments-list')?.scrollIntoView({ behavior: 'instant', block: 'center' });" } | Out-Null
    Start-Sleep -Milliseconds 500

    $shot = Call-CDP $ws "Page.captureScreenshot" @{ format = "png" }
    [System.IO.File]::WriteAllBytes("f:\FaceStoneWebsite\scratch\verified_04_req34_attachments_cards.png", [Convert]::FromBase64String($shot.result.data))
    Write-Host "Screenshot saved: verified_04_req34_attachments_cards.png"
}
finally {
    $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", [System.Threading.CancellationToken]::None).Wait()
    $chromeProc.Kill()
    Remove-Item -Path $tempProfile -Recurse -Force -ErrorAction SilentlyContinue
}
