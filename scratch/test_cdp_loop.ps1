$tempProfile = Join-Path $env:TEMP "chrome_cdp_test_$(Get-Random)"
$chromeProc = Start-Process -FilePath "C:\Program Files\Google\Chrome\Application\chrome.exe" -ArgumentList @("--headless=new", "--remote-debugging-port=9255", "--user-data-dir=$tempProfile", "http://localhost:5124") -PassThru
Start-Sleep -Seconds 2

$tabsJson = (New-Object System.Net.WebClient).DownloadString("http://localhost:9255/json")
$tabs = $tabsJson | ConvertFrom-Json
$pageTab = $tabs | Where-Object { $_.type -eq "page" } | Select-Object -First 1

$ws = [System.Net.WebSockets.ClientWebSocket]::new()
$ws.ConnectAsync([uri]$pageTab.webSocketDebuggerUrl, [System.Threading.CancellationToken]::None).Wait()

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
        if ($resp.id -eq $curId) {
            return $resp
        }
    }
}

$evalRes = Call-CDP $ws "Runtime.evaluate" @{ expression = "document.title"; returnByValue = $true }
Write-Host "Title: $($evalRes.result.result.value)"

$ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", [System.Threading.CancellationToken]::None).Wait()
$chromeProc.Kill()
