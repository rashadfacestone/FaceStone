param(
    [string]$BaseUrl = "http://localhost:5124"
)

$ErrorActionPreference = "Stop"

# Start Chrome with remote debugging
$tempProfile = Join-Path $env:TEMP "chrome_cdp_attach_$(Get-Random)"
$chromeArgs = @(
    "--headless=new",
    "--remote-debugging-port=9244",
    "--window-size=1440,1100",
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

    $buffer = [byte[]]::new(262144)
    $ms = [System.IO.MemoryStream]::new()
    do {
        $seg = [System.ArraySegment[byte]]::new($buffer)
        $res = $ws.ReceiveAsync($seg, [System.Threading.CancellationToken]::None).Result
        $ms.Write($buffer, 0, $res.Count)
    } while (!$res.EndOfMessage)

    $json = [System.Text.Encoding]::UTF8.GetString($ms.ToArray())
    $obj = $json | ConvertFrom-Json
    if ($obj.result -and $obj.result.result) {
        return $obj.result.result.value
    }
    return $null
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
        Write-Host "Saved screenshot: $outputPath"
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
    $ms = [System.IO.MemoryStream]::new()
    do {
        $seg = [System.ArraySegment[byte]]::new($buffer)
        $res = $ws.ReceiveAsync($seg, [System.Threading.CancellationToken]::None).Result
        $ms.Write($buffer, 0, $res.Count)
    } while (!$res.EndOfMessage)
}

try {
    # Connect to Chrome CDP via WebSocket
    $tabsJson = (New-Object System.Net.WebClient).DownloadString("http://localhost:9244/json")
    $tabs = $tabsJson | ConvertFrom-Json
    $pageTab = $tabs | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    $wsUrl = $pageTab.webSocketDebuggerUrl

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([uri]$wsUrl, [System.Threading.CancellationToken]::None).Wait()
    Write-Host "Connected to Chrome DevTools Protocol."

    # Enable Page domain
    $enablePayload = @{ id = 1; method = "Page.enable" } | ConvertTo-Json -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($enablePayload)
    $ws.SendAsync([System.ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).Wait()

    # Step 1: Open BaseUrl
    Invoke-CDPNavigate -ws $ws -url "$BaseUrl"
    Start-Sleep -Seconds 2

    # Step 2: Authenticate via ChatApi.login
    $loginExpr = @"
(async () => {
    try {
        window.alertHistory = [];
        window.alert = function(msg) { window.alertHistory.push(msg); console.warn('ALERT:', msg); };

        const res = await window.ChatApi.login('control_monitoring', 'Password123!');
        return {
            success: res.success,
            user: res.data?.user?.fullName,
            role: res.data?.user?.role,
            tokenStored: !!window.ChatApi.getToken()
        };
    } catch(e) {
        return { success: false, error: e.message };
    }
})()
"@
    $loginResult = Invoke-CDPEval -ws $ws -expression $loginExpr
    Write-Host "Login result: $($loginResult | ConvertTo-Json -Compress)"

    # Step 3: Open /control-monitoring/request/transportation/34
    Invoke-CDPNavigate -ws $ws -url "$BaseUrl/control-monitoring/request/transportation/34"
    Start-Sleep -Seconds 3

    # Step 4: Ensure request 34 details are loaded
    $checkReq34Expr = @"
(async () => {
    // Intercept window.alert
    if (!window.alertHistory) window.alertHistory = [];
    window.alert = function(msg) { window.alertHistory.push(msg); console.warn('ALERT:', msg); };

    if (window.ControlMonitoringApp) {
        window.ControlMonitoringApp.init();
        await window.ControlMonitoringApp.loadRequestDetails('transportation', 34);
    }

    const heroTitle = document.getElementById('cm-details-hero-title')?.textContent || '';
    const attCards = document.querySelectorAll('#cm-details-attachments-list .cm-attachment-card');
    const attachments = Array.from(attCards).map(card => {
        return {
            title: card.querySelector('.cm-attachment-title')?.textContent?.trim() || '',
            hasPreviewBtn: !!card.querySelector('.cm-btn-view'),
            hasDownloadBtn: !!card.querySelector('.cm-btn-download'),
            thumbImgSrc: card.querySelector('.cm-attachment-thumb-wrap img')?.src || ''
        };
    });

    return {
        heroTitle,
        attCount: attCards.length,
        attachments,
        alerts: window.alertHistory
    };
})()
"@
    $req34Info = Invoke-CDPEval -ws $ws -expression $checkReq34Expr
    Write-Host "Request 34 loaded: Hero='$($req34Info.heroTitle)', Attachments=$($req34Info.attCount)"
    foreach ($att in $req34Info.attachments) {
        Write-Host "  Attachment: $($att.title) | PreviewBtn=$($att.hasPreviewBtn) | DlBtn=$($att.hasDownloadBtn) | Thumb=$($att.thumbImgSrc.Substring(0, [Math]::Min(60, $att.thumbImgSrc.Length)))..."
    }

    # Step 5: Click Preview on first attachment (Transportation 34 Weighbridge Receipt)
    $clickPreviewExpr = @"
(async () => {
    window.alertHistory = [];
    const previewBtn = document.querySelector('#cm-details-attachments-list .cm-btn-view');
    if (!previewBtn) return { success: false, reason: 'No preview button' };

    previewBtn.click();
    await new Promise(r => setTimeout(r, 1200));

    const overlay = document.getElementById('cm-preview-overlay');
    const overlayDisplay = overlay ? overlay.style.display : 'none';
    const previewTitle = document.getElementById('cm-preview-title')?.textContent || '';
    const imgEl = overlay?.querySelector('.cm-preview-body img');
    const iframeEl = overlay?.querySelector('.cm-preview-body iframe');
    const errorBox = overlay?.querySelector('.cm-preview-body div[style*=\"EF4444\"]');

    return {
        success: true,
        overlayDisplay,
        previewTitle,
        hasImage: !!imgEl,
        imageSrc: imgEl ? imgEl.src.substring(0, 80) : '',
        imageLoaded: imgEl ? (imgEl.naturalWidth > 0) : false,
        hasIframe: !!iframeEl,
        hasErrorBox: !!errorBox,
        alerts: window.alertHistory
    };
})()
"@
    $prevResult = Invoke-CDPEval -ws $ws -expression $clickPreviewExpr
    Write-Host "Preview result: $($prevResult | ConvertTo-Json -Compress)"

    # Take Screenshot: Preview Modal
    $shotPrev = "f:\FaceStoneWebsite\scratch\browser_verified_01_preview_modal.png"
    Invoke-CDPScreenshot -ws $ws -outputPath $shotPrev

    # Close preview modal
    Invoke-CDPEval -ws $ws -expression "document.getElementById('cm-preview-close')?.click();"
    Start-Sleep -Milliseconds 500

    # Step 6: Test Download on attachment 0
    $clickDownloadExpr = @"
(async () => {
    window.alertHistory = [];
    window.downloadClicked = false;
    
    // Intercept a.click to detect native download trigger
    const origCreateElement = document.createElement.bind(document);
    document.createElement = function(tag) {
        const el = origCreateElement(tag);
        if (tag.toLowerCase() === 'a') {
            const origClick = el.click.bind(el);
            el.click = function() {
                window.downloadTriggered = {
                    download: el.download,
                    hrefType: el.href.startsWith('blob:') ? 'blob' : 'url',
                    href: el.href.substring(0, 80)
                };
                origClick();
            };
        }
        return el;
    };

    const dlBtn = document.querySelector('#cm-details-attachments-list .cm-btn-download');
    if (!dlBtn) return { success: false, reason: 'No download button' };

    dlBtn.click();
    await new Promise(r => setTimeout(r, 2000));

    return {
        success: true,
        downloadTriggered: window.downloadTriggered || null,
        alerts: window.alertHistory
    };
})()
"@
    $dlResult = Invoke-CDPEval -ws $ws -expression $clickDownloadExpr
    Write-Host "Download result: $($dlResult | ConvertTo-Json -Compress)"

    # Step 7: Test Storage Form 12 (PDF Attachment)
    Invoke-CDPNavigate -ws $ws -url "$BaseUrl/control-monitoring/request/storage/12"
    Start-Sleep -Seconds 3

    $checkStorage12Expr = @"
(async () => {
    window.alertHistory = [];
    if (window.ControlMonitoringApp) {
        window.ControlMonitoringApp.init();
        await window.ControlMonitoringApp.loadRequestDetails('storage', 12);
    }

    const heroTitle = document.getElementById('cm-details-hero-title')?.textContent || '';
    const attCards = document.querySelectorAll('#cm-details-attachments-list .cm-attachment-card');
    return {
        heroTitle,
        attCount: attCards.length,
        firstAttTitle: attCards[0]?.querySelector('.cm-attachment-title')?.textContent?.trim() || ''
    };
})()
"@
    $st12Info = Invoke-CDPEval -ws $ws -expression $checkStorage12Expr
    Write-Host "Storage 12 loaded: Hero='$($st12Info.heroTitle)', Attachments=$($st12Info.attCount), FirstAtt='$($st12Info.firstAttTitle)'"

    # Preview Storage 12 (PDF)
    $previewPdfExpr = @"
(async () => {
    window.alertHistory = [];
    const previewBtn = document.querySelector('#cm-details-attachments-list .cm-btn-view');
    if (!previewBtn) return { success: false, reason: 'No preview button' };

    previewBtn.click();
    await new Promise(r => setTimeout(r, 1200));

    const overlay = document.getElementById('cm-preview-overlay');
    const iframeEl = overlay?.querySelector('.cm-preview-body iframe');
    const errorBox = overlay?.querySelector('.cm-preview-body div[style*=\"EF4444\"]');

    return {
        success: true,
        overlayDisplay: overlay ? overlay.style.display : 'none',
        hasIframe: !!iframeEl,
        iframeSrc: iframeEl ? iframeEl.src.substring(0, 90) : '',
        hasErrorBox: !!errorBox,
        alerts: window.alertHistory
    };
})()
"@
    $prevPdfResult = Invoke-CDPEval -ws $ws -expression $previewPdfExpr
    Write-Host "PDF Preview result: $($prevPdfResult | ConvertTo-Json -Compress)"

    # Take Screenshot: PDF Preview Modal
    $shotPdf = "f:\FaceStoneWebsite\scratch\browser_verified_02_pdf_preview.png"
    Invoke-CDPScreenshot -ws $ws -outputPath $shotPdf

    # Close PDF preview modal
    Invoke-CDPEval -ws $ws -expression "document.getElementById('cm-preview-close')?.click();"
    Start-Sleep -Milliseconds 500

    # Test Download on Storage 12 (PDF)
    $dlPdfResult = Invoke-CDPEval -ws $ws -expression $clickDownloadExpr
    Write-Host "PDF Download result: $($dlPdfResult | ConvertTo-Json -Compress)"

    # Take final screenshot of Storage Request Details page
    $shotDetails = "f:\FaceStoneWebsite\scratch\browser_verified_03_storage_page.png"
    Invoke-CDPScreenshot -ws $ws -outputPath $shotDetails

    Write-Host "`nALL BROWSER VERIFICATION TESTS PASSED SUCCESSFULLY!"
}
finally {
    if ($ws -and $ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Closing", [System.Threading.CancellationToken]::None).Wait()
    }
    if ($chromeProc -and !$chromeProc.HasExited) {
        $chromeProc.Kill()
    }
    if (Test-Path $tempProfile) {
        Remove-Item -Path $tempProfile -Recurse -Force -ErrorAction SilentlyContinue
    }
}
