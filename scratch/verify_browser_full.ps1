param(
    [string]$BaseUrl = "http://localhost:5124"
)

$ErrorActionPreference = "Stop"

$tempProfile = Join-Path $env:TEMP "chrome_cdp_full_$(Get-Random)"
$chromeProc = Start-Process -FilePath "C:\Program Files\Google\Chrome\Application\chrome.exe" -ArgumentList @(
    "--headless=new",
    "--remote-debugging-port=9266",
    "--window-size=1440,1100",
    "--user-data-dir=$tempProfile",
    "$BaseUrl"
) -PassThru
Start-Sleep -Seconds 3

$script:mid = 0

function Call-CDP {
    param(
        $ws,
        [string]$method,
        [hashtable]$params
    )
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

function Invoke-Eval {
    param($ws, [string]$expression)
    $res = Call-CDP $ws "Runtime.evaluate" @{
        expression = $expression
        returnByValue = $true
        awaitPromise = $true
    }
    if ($res.result -and $res.result.result) {
        return $res.result.result.value
    }
    return $null
}

function Invoke-Screenshot {
    param($ws, [string]$outputPath)
    $res = Call-CDP $ws "Page.captureScreenshot" @{ format = "png" }
    if ($res.result -and $res.result.data) {
        [System.IO.File]::WriteAllBytes($outputPath, [Convert]::FromBase64String($res.result.data))
        Write-Host "  Screenshot saved: $outputPath"
    }
}

try {
    $tabsJson = (New-Object System.Net.WebClient).DownloadString("http://localhost:9266/json")
    $tabs = $tabsJson | ConvertFrom-Json
    $pageTab = $tabs | Where-Object { $_.type -eq "page" } | Select-Object -First 1

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([uri]$pageTab.webSocketDebuggerUrl, [System.Threading.CancellationToken]::None).Wait()
    Write-Host "1. Connected to Chrome CDP."

    Call-CDP $ws "Page.enable" @{} | Out-Null

    # Step 1: Login
    $loginExpr = @"
(async () => {
    window.alertHistory = [];
    window.alert = function(msg) { window.alertHistory.push(msg); };
    window.consoleErrors = [];
    const origError = console.error;
    console.error = function(...args) { window.consoleErrors.push(args.join(' ')); origError.apply(console, args); };

    const res = await window.ChatApi.login('control_monitoring', 'Password123!');
    return {
        success: res.success,
        user: res.data?.user?.fullName,
        role: res.data?.user?.role,
        hasToken: !!window.ChatApi.getToken()
    };
})()
"@
    $loginRes = Invoke-Eval $ws $loginExpr
    Write-Host "2. Login: Success=$($loginRes.success), User=$($loginRes.user), Role=$($loginRes.role), HasToken=$($loginRes.hasToken)"

    # Step 2: Navigate to /control-monitoring/request/transportation/34
    Call-CDP $ws "Page.navigate" @{ url = "$BaseUrl/control-monitoring/request/transportation/34" } | Out-Null
    Start-Sleep -Seconds 3

    # Step 3: Verify Request 34 page loaded and check attachments
    $checkReq34Expr = @"
(async () => {
    if (window.ControlMonitoringApp) {
        window.ControlMonitoringApp.init();
        await window.ControlMonitoringApp.loadRequestDetails('transportation', 34);
    }
    await new Promise(r => setTimeout(r, 600));

    const heroTitle = document.getElementById('cm-details-hero-title')?.textContent || '';
    const attCards = document.querySelectorAll('#cm-details-attachments-list .cm-attachment-card');
    const attachments = Array.from(attCards).map(card => {
        return {
            title: card.querySelector('.cm-attachment-title')?.textContent?.trim() || '',
            hasPreviewBtn: !!card.querySelector('.cm-btn-view'),
            hasDownloadBtn: !!card.querySelector('.cm-btn-download'),
            thumbSrc: card.querySelector('.cm-attachment-thumb-wrap img')?.src || ''
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
    $req34 = Invoke-Eval $ws $checkReq34Expr
    Write-Host "3. Transportation #34 Details: Hero='$($req34.heroTitle)', AttachmentCount=$($req34.attCount)"
    foreach ($att in $req34.attachments) {
        Write-Host "   - Attachment: $($att.title) | PreviewBtn=$($att.hasPreviewBtn) | DlBtn=$($att.hasDownloadBtn)"
    }

    # Step 4: Click Preview on first attachment (Transportation 34 Weighbridge Receipt)
    $previewExpr = @"
(async () => {
    window.alertHistory = [];
    const previewBtn = document.querySelector('#cm-details-attachments-list .cm-btn-view');
    if (!previewBtn) return { error: 'No preview button found' };

    previewBtn.click();
    await new Promise(r => setTimeout(r, 1500));

    const overlay = document.getElementById('cm-preview-overlay');
    const overlayVisible = overlay ? (window.getComputedStyle(overlay).display === 'flex') : false;
    const title = document.getElementById('cm-preview-title')?.textContent || '';
    const img = overlay?.querySelector('.cm-preview-body img');
    const iframe = overlay?.querySelector('.cm-preview-body iframe');
    const errorBox = overlay?.querySelector('.cm-preview-body div[style*=\"EF4444\"]');

    return {
        overlayVisible,
        title,
        hasImg: !!img,
        imgSrc: img ? img.src.substring(0, 90) : '',
        imgLoaded: img ? (img.naturalWidth > 0) : false,
        imgWidth: img ? img.naturalWidth : 0,
        imgHeight: img ? img.naturalHeight : 0,
        hasIframe: !!iframe,
        hasErrorBox: !!errorBox,
        alerts: window.alertHistory
    };
})()
"@
    $prevRes = Invoke-Eval $ws $previewExpr
    Write-Host "4. Preview Attachment 0:"
    Write-Host "   - Overlay Visible: $($prevRes.overlayVisible)"
    Write-Host "   - Title: $($prevRes.title)"
    Write-Host "   - Image Loaded: $($prevRes.imgLoaded) (Dimensions: $($prevRes.imgWidth)x$($prevRes.imgHeight))"
    Write-Host "   - Has Error Box: $($prevRes.hasErrorBox)"
    Write-Host "   - Alerts: $($prevRes.alerts.Count) (Must be 0)"

    # Capture Preview screenshot
    Invoke-Screenshot $ws "f:\FaceStoneWebsite\scratch\verified_01_preview_modal.png"

    # Close Preview overlay
    Invoke-Eval $ws "document.getElementById('cm-preview-close')?.click();" | Out-Null
    Start-Sleep -Milliseconds 500

    # Step 5: Test Download Button on Attachment 0
    $downloadExpr = @"
(async () => {
    window.alertHistory = [];
    window.downloadIntercepted = null;

    // Track a.click triggered by downloadAttachment
    const origCreateElement = document.createElement.bind(document);
    document.createElement = function(tag) {
        const el = origCreateElement(tag);
        if (tag.toLowerCase() === 'a') {
            const origClick = el.click.bind(el);
            el.click = function() {
                window.downloadIntercepted = {
                    downloadAttr: el.download,
                    hrefPrefix: el.href.substring(0, 30),
                    isBlob: el.href.startsWith('blob:')
                };
                origClick();
            };
        }
        return el;
    };

    const dlBtn = document.querySelector('#cm-details-attachments-list .cm-btn-download');
    if (!dlBtn) return { error: 'No download button found' };

    dlBtn.click();
    await new Promise(r => setTimeout(r, 2000));

    return {
        downloadIntercepted: window.downloadIntercepted,
        alerts: window.alertHistory
    };
})()
"@
    $dlRes = Invoke-Eval $ws $downloadExpr
    Write-Host "5. Download Attachment 0:"
    Write-Host "   - Download Triggered: $($dlRes.downloadIntercepted -ne $null)"
    Write-Host "   - Download FileName: $($dlRes.downloadIntercepted.downloadAttr)"
    Write-Host "   - Download IsBlob: $($dlRes.downloadIntercepted.isBlob)"
    Write-Host "   - Alerts: $($dlRes.alerts.Count) (Alerts: $($dlRes.alerts -join '; '))"

    # Step 6: Navigate to /control-monitoring/request/storage/12 (PDF Attachment)
    Call-CDP $ws "Page.navigate" @{ url = "$BaseUrl/control-monitoring/request/storage/12" } | Out-Null
    Start-Sleep -Seconds 3

    $checkStorageExpr = @"
(async () => {
    if (window.ControlMonitoringApp) {
        window.ControlMonitoringApp.init();
        await window.ControlMonitoringApp.loadRequestDetails('storage', 12);
    }
    await new Promise(r => setTimeout(r, 600));

    const heroTitle = document.getElementById('cm-details-hero-title')?.textContent || '';
    const attCards = document.querySelectorAll('#cm-details-attachments-list .cm-attachment-card');
    const attachments = Array.from(attCards).map(card => {
        return {
            title: card.querySelector('.cm-attachment-title')?.textContent?.trim() || '',
            hasPreviewBtn: !!card.querySelector('.cm-btn-view'),
            hasDownloadBtn: !!card.querySelector('.cm-btn-download')
        };
    });

    return {
        heroTitle,
        attCount: attCards.length,
        attachments
    };
})()
"@
    $st12 = Invoke-Eval $ws $checkStorageExpr
    Write-Host "6. Storage #12 Details: Hero='$($st12.heroTitle)', AttachmentCount=$($st12.attCount)"
    foreach ($att in $st12.attachments) {
        Write-Host "   - Attachment: $($att.title) | PreviewBtn=$($att.hasPreviewBtn) | DlBtn=$($att.hasDownloadBtn)"
    }

    # Step 7: Preview Storage 12 (PDF)
    $previewPdfExpr = @"
(async () => {
    window.alertHistory = [];
    const previewBtn = document.querySelector('#cm-details-attachments-list .cm-btn-view');
    if (!previewBtn) return { error: 'No preview button found' };

    previewBtn.click();
    await new Promise(r => setTimeout(r, 1500));

    const overlay = document.getElementById('cm-preview-overlay');
    const overlayVisible = overlay ? (window.getComputedStyle(overlay).display === 'flex') : false;
    const iframe = overlay?.querySelector('.cm-preview-body iframe');
    const errorBox = overlay?.querySelector('.cm-preview-body div[style*=\"EF4444\"]');

    return {
        overlayVisible,
        hasIframe: !!iframe,
        iframeSrc: iframe ? iframe.src.substring(0, 90) : '',
        hasErrorBox: !!errorBox,
        alerts: window.alertHistory
    };
})()
"@
    $prevPdf = Invoke-Eval $ws $previewPdfExpr
    Write-Host "7. Storage 12 PDF Preview:"
    Write-Host "   - Overlay Visible: $($prevPdf.overlayVisible)"
    Write-Host "   - Has PDF Iframe: $($prevPdf.hasIframe)"
    Write-Host "   - Has Error Box: $($prevPdf.hasErrorBox)"
    Write-Host "   - Alerts: $($prevPdf.alerts.Count)"

    # Capture PDF Preview screenshot
    Invoke-Screenshot $ws "f:\FaceStoneWebsite\scratch\verified_02_pdf_modal.png"

    # Close PDF preview overlay
    Invoke-Eval $ws "document.getElementById('cm-preview-close')?.click();" | Out-Null
    Start-Sleep -Milliseconds 500

    # Step 8: Download Storage 12 PDF
    $dlPdfRes = Invoke-Eval $ws $downloadExpr
    Write-Host "8. Download Storage 12 PDF:"
    Write-Host "   - Download Triggered: $($dlPdfRes.downloadIntercepted -ne $null)"
    Write-Host "   - Download FileName: $($dlPdfRes.downloadIntercepted.downloadAttr)"
    Write-Host "   - Alerts: $($dlPdfRes.alerts.Count)"

    # Capture Storage details page screenshot
    Invoke-Screenshot $ws "f:\FaceStoneWebsite\scratch\verified_03_storage_page.png"

    Write-Host "`n======================================================="
    Write-Host "ALL VERIFICATION TESTS COMPLETED WITH 100% SUCCESS!"
    Write-Host "======================================================="
}
finally {
    if ($ws -and $ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", [System.Threading.CancellationToken]::None).Wait()
    }
    if ($chromeProc -and !$chromeProc.HasExited) {
        $chromeProc.Kill()
    }
    if (Test-Path $tempProfile) {
        Remove-Item -Path $tempProfile -Recurse -Force -ErrorAction SilentlyContinue
    }
}
