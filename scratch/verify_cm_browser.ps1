param(
    [string]$BaseUrl = "http://localhost:5124"
)

$ErrorActionPreference = "Stop"

# 1. Start Chrome with remote debugging
$tempProfile = Join-Path $env:TEMP "chrome_cdp_cm_$(Get-Random)"
$chromeArgs = @(
    "--headless=new",
    "--remote-debugging-port=9233",
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
    # 2. Connect to Chrome CDP via WebSocket
    $tabsJson = (New-Object System.Net.WebClient).DownloadString("http://localhost:9233/json")
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

    # 3. Log in via API to set localStorage token and user session
    Invoke-CDPNavigate -ws $ws -url "$BaseUrl"
    Start-Sleep -Seconds 2

    $loginExpr = @"
(async () => {
    try {
        const res = await fetch('/api/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ identifier: 'control_monitoring', password: 'Password123!' })
        });
        const data = await res.json();
        if (data.success && data.data) {
            localStorage.setItem('facestone_token', data.data.token);
            localStorage.setItem('facestone_user', JSON.stringify(data.data.user));
            return { success: true, user: data.data.user.fullName };
        }
        return { success: false, msg: data.message };
    } catch (e) {
        return { success: false, err: e.message };
    }
})()
"@
    $loginResult = Invoke-CDPEval -ws $ws -expression $loginExpr
    Write-Host "Browser session login result: $($loginResult | ConvertTo-Json -Compress)"

    # 4. Navigate directly to /control-monitoring
    Invoke-CDPNavigate -ws $ws -url "$BaseUrl/control-monitoring"
    Start-Sleep -Seconds 3

    # Ensure ControlMonitoring department is open in ChatApp
    $ensureDeptExpr = @"
(async () => {
    if (window.ControlMonitoringApp) {
        window.ControlMonitoringApp.init();
        window.ControlMonitoringApp.loadStats();
        window.ControlMonitoringApp.loadForms();
    }
    const root = document.getElementById('control-monitoring-page-root');
    if (root) root.style.display = 'block';
    const mainView = document.getElementById('control-monitoring-main-view');
    if (mainView) mainView.style.display = 'block';
    const detailsView = document.getElementById('control-monitoring-request-details-view');
    if (detailsView) detailsView.style.display = 'none';

    // Wait a brief moment for forms to populate
    await new Promise(r => setTimeout(r, 600));

    const rowsCount = document.querySelectorAll('#cm-forms-table-body tr').length;
    const title = document.querySelector('.finance-mgmt-title-content h1')?.textContent || '';
    return { title, rowsCount };
})()
"@
    $dashInfo = Invoke-CDPEval -ws $ws -expression $ensureDeptExpr
    Write-Host "Dashboard info: Title='$($dashInfo.title)', Rows=$($dashInfo.rowsCount)"

    # Take Screenshot 1: Main Dashboard
    $shot1 = "f:\FaceStoneWebsite\scratch\cm_browser_01_dashboard.png"
    Invoke-CDPScreenshot -ws $ws -outputPath $shot1

    # 5. Click [ عرض التفاصيل ← ] on the first request (Transportation ID 34)
    $openDetailsExpr = @"
(() => {
    const btn = document.querySelector('.cm-btn-open-request-details');
    if (btn) {
        btn.click();
        return { clicked: true, id: btn.getAttribute('data-id'), type: btn.getAttribute('data-type') };
    }
    return { clicked: false };
})()
"@
    $openRes = Invoke-CDPEval -ws $ws -expression $openDetailsExpr
    Write-Host "Open details clicked: $($openRes | ConvertTo-Json -Compress)"
    Start-Sleep -Seconds 2

    # Check details page content
    $checkDetailsExpr = @"
(() => {
    const mainVis = document.getElementById('control-monitoring-main-view')?.style.display;
    const detVis = document.getElementById('control-monitoring-request-details-view')?.style.display;
    const heroTitle = document.getElementById('cm-details-hero-title')?.textContent || '';
    const statusText = document.getElementById('cm-details-hero-status-badge')?.textContent || '';
    const attCount = document.querySelectorAll('#cm-details-attachments-list .cm-attachment-card').length;
    const fieldsCount = document.querySelectorAll('#cm-details-form-data-body .cm-field-item').length;
    const hasAcceptBtn = !!document.getElementById('cm-details-btn-accept');
    const hasRejectBtn = !!document.getElementById('cm-details-btn-reject');
    const timelineEvents = document.querySelectorAll('#cm-details-timeline .cm-timeline-item').length;
    return { mainVis, detVis, heroTitle, statusText, attCount, fieldsCount, hasAcceptBtn, hasRejectBtn, timelineEvents };
})()
"@
    $detInfo = Invoke-CDPEval -ws $ws -expression $checkDetailsExpr
    Write-Host "Details View Info: Hero='$($detInfo.heroTitle)', Status='$($detInfo.statusText)', Fields=$($detInfo.fieldsCount), Attachments=$($detInfo.attCount), AcceptBtn=$($detInfo.hasAcceptBtn), RejectBtn=$($detInfo.hasRejectBtn), Timeline=$($detInfo.timelineEvents)"

    # Take Screenshot 2: Dedicated Request Details Page (Transportation)
    $shot2 = "f:\FaceStoneWebsite\scratch\cm_browser_02_trans_details.png"
    Invoke-CDPScreenshot -ws $ws -outputPath $shot2

    # 6. Test Attachment Preview Overlay
    $openPreviewExpr = @"
(() => {
    const previewBtn = document.querySelector('.cm-btn-view');
    if (previewBtn) {
        previewBtn.click();
        return { clicked: true };
    }
    return { clicked: false };
})()
"@
    $prevRes = Invoke-CDPEval -ws $ws -expression $openPreviewExpr
    Write-Host "Attachment preview click: $($prevRes | ConvertTo-Json -Compress)"
    Start-Sleep -Seconds 1

    # Take Screenshot 3: Attachment Preview Overlay
    $shot3 = "f:\FaceStoneWebsite\scratch\cm_browser_03_preview_modal.png"
    Invoke-CDPScreenshot -ws $ws -outputPath $shot3

    # Close Preview Overlay
    Invoke-CDPEval -ws $ws -expression "document.getElementById('cm-preview-close')?.click();"
    Start-Sleep -Milliseconds 500

    # 7. Test Clicking [ ✓ قبول واعتماد النموذج (ACCEPT) ] to verify confirmation drawer
    $triggerAcceptExpr = @"
(() => {
    const btn = document.getElementById('cm-details-btn-accept');
    if (btn) {
        btn.click();
        const box = document.getElementById('cm-details-accept-box');
        return { clicked: true, boxDisplay: box?.style.display };
    }
    return { clicked: false };
})()
"@
    $acceptBoxRes = Invoke-CDPEval -ws $ws -expression $triggerAcceptExpr
    Write-Host "Accept confirmation box opened: $($acceptBoxRes | ConvertTo-Json -Compress)"
    Start-Sleep -Milliseconds 600

    # Take Screenshot 4: Accept confirmation drawer
    $shot4 = "f:\FaceStoneWebsite\scratch\cm_browser_04_accept_drawer.png"
    Invoke-CDPScreenshot -ws $ws -outputPath $shot4

    # Cancel Accept drawer
    Invoke-CDPEval -ws $ws -expression "document.getElementById('cm-details-btn-cancel-accept')?.click();"
    Start-Sleep -Milliseconds 300

    # 8. Test Clicking [ ✕ رفض النموذج للتصحيح (REJECT) ] to verify rejection drawer
    $triggerRejectExpr = @"
(() => {
    const btn = document.getElementById('cm-details-btn-reject');
    if (btn) {
        btn.click();
        const box = document.getElementById('cm-details-reject-box');
        return { clicked: true, boxDisplay: box?.style.display };
    }
    return { clicked: false };
})()
"@
    $rejectBoxRes = Invoke-CDPEval -ws $ws -expression $triggerRejectExpr
    Write-Host "Reject drawer opened: $($rejectBoxRes | ConvertTo-Json -Compress)"
    Start-Sleep -Milliseconds 600

    # Take Screenshot 5: Reject drawer
    $shot5 = "f:\FaceStoneWebsite\scratch\cm_browser_05_reject_drawer.png"
    Invoke-CDPScreenshot -ws $ws -outputPath $shot5

    # 9. Test Back Button: Click [ ← العودة إلى قائمة الطلبات ]
    $backExpr = @"
(() => {
    const backBtn = document.getElementById('cm-details-back-btn');
    if (backBtn) {
        backBtn.click();
        return { clicked: true };
    }
    return { clicked: false };
})()
"@
    $backRes = Invoke-CDPEval -ws $ws -expression $backExpr
    Write-Host "Back button clicked: $($backRes | ConvertTo-Json -Compress)"
    Start-Sleep -Seconds 1

    # 10. Switch filter to Storage and open details of a Storage Request (e.g. ID 28)
    $openStorageExpr = @"
(async () => {
    // Select Storage from dropdown
    const select = document.getElementById('cm-filter-type-select');
    if (select) {
        select.value = 'Storage';
        select.dispatchEvent(new Event('change'));
    }
    await new Promise(r => setTimeout(r, 600));

    // Find first row
    const btn = document.querySelector('.cm-btn-open-request-details');
    if (btn) {
        btn.click();
        return { clicked: true, id: btn.getAttribute('data-id'), type: btn.getAttribute('data-type') };
    }
    return { clicked: false };
})()
"@
    $storageOpenRes = Invoke-CDPEval -ws $ws -expression $openStorageExpr
    Write-Host "Storage open details result: $($storageOpenRes | ConvertTo-Json -Compress)"
    Start-Sleep -Seconds 2

    # Check Storage Form Data fields rendered
    $checkStorageExpr = @"
(() => {
    const heroTitle = document.getElementById('cm-details-hero-title')?.textContent || '';
    const statusText = document.getElementById('cm-details-hero-status-badge')?.textContent || '';
    const fields = Array.from(document.querySelectorAll('#cm-details-form-data-body .cm-field-item')).map(el => {
        return (el.querySelector('.cm-field-label')?.textContent || '') + ': ' + (el.querySelector('.cm-field-value')?.textContent || '');
    });
    return { heroTitle, statusText, fieldsCount: fields.length, sampleFields: fields.slice(0, 8) };
})()
"@
    $stInfo = Invoke-CDPEval -ws $ws -expression $checkStorageExpr
    Write-Host "Storage Details Info: Hero='$($stInfo.heroTitle)', Status='$($stInfo.statusText)', Fields=$($stInfo.fieldsCount)"
    foreach ($f in $stInfo.sampleFields) {
        Write-Host "   $f"
    }

    # Take Screenshot 6: Storage Request Details
    $shot6 = "f:\FaceStoneWebsite\scratch\cm_browser_06_storage_details.png"
    Invoke-CDPScreenshot -ws $ws -outputPath $shot6

    Write-Host "`nAll browser verification steps completed successfully!"
}
finally {
    if ($ws -and $ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Closing", [System.Threading.CancellationToken]::None).Wait()
    }
    if ($chromeProc -and !$chromeProc.HasExited) {
        $chromeProc.Kill()
    }
    if (Test-Path $tempProfile) {
        Remove-Item -Recurse -Force $tempProfile -ErrorAction SilentlyContinue
    }
}
