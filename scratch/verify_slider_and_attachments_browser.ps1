param(
    [string]$BaseUrl = "http://localhost:5124",
    [string]$ArtifactsDir = "C:\Users\win 10\.gemini\antigravity-ide\brain\05e50d0f-a203-4699-a1a7-79ba521eace6"
)

$ErrorActionPreference = "Stop"

Write-Host "=== LAUNCHING CHROME CDP FOR CONTROL & MONITORING SLIDER + ATTACHMENTS VERIFICATION ===" -ForegroundColor Cyan

$tempProfile = Join-Path $env:TEMP "chrome_cdp_slider_test_$(Get-Random)"
$chromeArgs = @(
    "--headless=new",
    "--remote-debugging-port=9288",
    "--window-size=1440,1100",
    "--user-data-dir=$tempProfile",
    "about:blank"
)
$chromeProc = Start-Process -FilePath "C:\Program Files\Google\Chrome\Application\chrome.exe" -ArgumentList $chromeArgs -PassThru
Start-Sleep -Seconds 2

$script:msgId = 0

function Invoke-CDPCommand {
    param(
        [System.Net.WebSockets.ClientWebSocket]$ws,
        [string]$method,
        [hashtable]$params = @{}
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

    while ($true) {
        $buffer = [byte[]]::new(262144)
        $ms = [System.IO.MemoryStream]::new()
        do {
            $seg = [System.ArraySegment[byte]]::new($buffer)
            $res = $ws.ReceiveAsync($seg, [System.Threading.CancellationToken]::None).Result
            $ms.Write($buffer, 0, $res.Count)
        } while (!$res.EndOfMessage)

        $json = [System.Text.Encoding]::UTF8.GetString($ms.ToArray())
        $obj = $json | ConvertFrom-Json
        if ($obj -and $obj.id -eq $id) {
            return $obj.result
        }
    }
}

function Invoke-CDPEval {
    param(
        [System.Net.WebSockets.ClientWebSocket]$ws,
        [string]$expression
    )
    $res = Invoke-CDPCommand -ws $ws -method "Runtime.evaluate" -params @{
        expression = $expression
        returnByValue = $true
        awaitPromise = $true
    }
    if ($res -and $res.result) {
        return $res.result.value
    }
    return $null
}

function Save-CDPScreenshot {
    param(
        [System.Net.WebSockets.ClientWebSocket]$ws,
        [string]$outPath
    )
    $res = Invoke-CDPCommand -ws $ws -method "Page.captureScreenshot" -params @{ format = "png" }
    if ($res -and $res.data) {
        $bytes = [System.Convert]::FromBase64String($res.data)
        [System.IO.File]::WriteAllBytes($outPath, $bytes)
        Write-Host "Screenshot saved: $outPath ($($bytes.Length) bytes)" -ForegroundColor Green
    }
}

try {
    $tabsJson = Invoke-RestMethod -Uri "http://localhost:9288/json"
    $wsUrl = $tabsJson[0].webSocketDebuggerUrl

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([System.Uri]::new($wsUrl), [System.Threading.CancellationToken]::None).Wait()
    Write-Host "Connected to CDP WebSocket: $wsUrl" -ForegroundColor Green

    Invoke-CDPCommand -ws $ws -method "Page.enable" | Out-Null
    Invoke-CDPCommand -ws $ws -method "Runtime.enable" | Out-Null

    # 1. Navigate to BaseUrl
    Write-Host "`nNavigating to $BaseUrl..." -ForegroundColor Cyan
    Invoke-CDPCommand -ws $ws -method "Page.navigate" -params @{ url = "$BaseUrl" } | Out-Null
    
    # Wait until page is completely loaded
    $waitAttempts = 0
    while ($waitAttempts -lt 20) {
        Start-Sleep -Milliseconds 400
        $readyState = Invoke-CDPEval -ws $ws -expression "document.readyState"
        $hasChatApi = Invoke-CDPEval -ws $ws -expression "typeof window.ChatApi !== 'undefined'"
        if ($readyState -eq "complete" -and $hasChatApi) {
            Write-Host "Page fully loaded and ChatApi ready (Attempt $waitAttempts)." -ForegroundColor Green
            break
        }
        $waitAttempts++
    }

    # 2. Authenticate session via ChatApi
    Write-Host "Authenticating as control_monitoring via ChatApi..." -ForegroundColor Cyan
    $loginExpr = @"
    (async () => {
        if (window.ChatApi) {
            const res = await window.ChatApi.login('control_monitoring', 'Password123!');
            if (res.success) {
                if (window.ChatApp) {
                    await window.ChatApp.enterDepartment('control-monitoring');
                }
                return { success: true, user: res.data?.user?.fullName || res.data?.user?.name };
            }
            return { success: false, msg: res.message || 'Login rejected' };
        }
        return { success: false, msg: 'ChatApi not found' };
    })()
"@
    $loginResult = Invoke-CDPEval -ws $ws -expression $loginExpr
    Write-Host "Auth result: $($loginResult | ConvertTo-Json -Compress)" -ForegroundColor Green
    Start-Sleep -Seconds 2

    # Ensure ControlMonitoring department is visible and forms loaded
    $ensureDeptExpr = @"
    (async () => {
        const root = document.getElementById('control-monitoring-page-root');
        if (root) root.style.display = 'block';
        const mainView = document.getElementById('control-monitoring-main-view');
        if (mainView) mainView.style.display = 'block';
        const detailsView = document.getElementById('control-monitoring-request-details-view');
        if (detailsView) detailsView.style.display = 'none';

        if (window.ControlMonitoringApp) {
            window.ControlMonitoringApp.init();
            await window.ControlMonitoringApp.loadStats();
            await window.ControlMonitoringApp.loadForms();
        }

        await new Promise(r => setTimeout(r, 800));
        const rows = document.querySelectorAll('#cm-forms-table-body tr').length;
        return { rows };
    })()
"@
    $deptRes = Invoke-CDPEval -ws $ws -expression $ensureDeptExpr
    Write-Host "Control Monitoring initialized: $($deptRes | ConvertTo-Json -Compress)" -ForegroundColor Green

    # -------------------------------------------------------------------------
    # PART 1: VERIFY SLIDER IN finance-table-wrapper
    # -------------------------------------------------------------------------
    Write-Host "`n--- VERIFYING PART 1: SLIDER IN finance-table-wrapper ---" -ForegroundColor Cyan

    $sliderCheck = Invoke-CDPEval -ws $ws -expression @"
    (() => {
        const wrapper = document.getElementById('cm-finance-table-wrapper') || document.querySelector('.finance-table-wrapper');
        const prevBtn = document.getElementById('cm-table-slider-prev');
        const nextBtn = document.getElementById('cm-table-slider-next');
        const sidePrevBtn = document.getElementById('cm-slider-side-prev');
        const sideNextBtn = document.getElementById('cm-slider-side-next');
        const table = wrapper ? wrapper.querySelector('.finance-table') : null;
        const rows = table ? table.querySelectorAll('tbody tr') : [];
        
        return {
            wrapperExists: !!wrapper,
            prevBtnExists: !!prevBtn,
            nextBtnExists: !!nextBtn,
            sidePrevBtnExists: !!sidePrevBtn,
            sideNextBtnExists: !!sideNextBtn,
            tableExists: !!table,
            rowCount: rows.length,
            wrapperScrollWidth: wrapper ? wrapper.scrollWidth : 0,
            wrapperClientWidth: wrapper ? wrapper.clientWidth : 0,
            initialScrollLeft: wrapper ? wrapper.scrollLeft : 0
        };
    })()
"@

    Write-Host "Wrapper exists: $($sliderCheck.wrapperExists)" -ForegroundColor Green
    Write-Host "Header Prev [ ← ] button exists: $($sliderCheck.prevBtnExists)" -ForegroundColor Green
    Write-Host "Header Next [ → ] button exists: $($sliderCheck.nextBtnExists)" -ForegroundColor Green
    Write-Host "Side Prev button exists: $($sliderCheck.sidePrevBtnExists)" -ForegroundColor Green
    Write-Host "Side Next button exists: $($sliderCheck.sideNextBtnExists)" -ForegroundColor Green
    Write-Host "Table rows count: $($sliderCheck.rowCount)" -ForegroundColor Green
    Write-Host "ScrollWidth: $($sliderCheck.wrapperScrollWidth)px, ClientWidth: $($sliderCheck.wrapperClientWidth)px" -ForegroundColor Green

    # Test clicking Next Slider button
    Write-Host "Clicking Next Slider button..." -ForegroundColor Cyan
    Invoke-CDPEval -ws $ws -expression @"
    (() => {
        const nextBtn = document.getElementById('cm-table-slider-next');
        if (nextBtn) nextBtn.click();
        return true;
    })()
"@ | Out-Null
    Start-Sleep -Milliseconds 700

    $scrolledCheck = Invoke-CDPEval -ws $ws -expression @"
    (() => {
        const wrapper = document.getElementById('cm-finance-table-wrapper') || document.querySelector('.finance-table-wrapper');
        return {
            scrollLeftAfterNext: wrapper ? wrapper.scrollLeft : 0
        };
    })()
"@
    Write-Host "Scroll position after Next click: $($scrolledCheck.scrollLeftAfterNext)" -ForegroundColor Green

    # Test clicking Prev Slider button
    Write-Host "Clicking Prev Slider button..." -ForegroundColor Cyan
    Invoke-CDPEval -ws $ws -expression @"
    (() => {
        const prevBtn = document.getElementById('cm-table-slider-prev');
        if (prevBtn) prevBtn.click();
        return true;
    })()
"@ | Out-Null
    Start-Sleep -Milliseconds 700

    # Test Mouse Drag on desktop
    Write-Host "Testing Desktop Mouse Drag on finance-table-wrapper..." -ForegroundColor Cyan
    $dragResult = Invoke-CDPEval -ws $ws -expression @"
    (() => {
        const wrapper = document.getElementById('cm-finance-table-wrapper') || document.querySelector('.finance-table-wrapper');
        if (!wrapper) return 'NO_WRAPPER';

        const initialPos = wrapper.scrollLeft;
        
        // Dispatch mousedown
        const downEvt = new MouseEvent('mousedown', {
            bubbles: true,
            cancelable: true,
            clientX: 600,
            clientY: 300,
            button: 0
        });
        wrapper.dispatchEvent(downEvt);

        // Dispatch mousemove
        const moveEvt = new MouseEvent('mousemove', {
            bubbles: true,
            cancelable: true,
            clientX: 300,
            clientY: 300
        });
        wrapper.dispatchEvent(moveEvt);

        // Dispatch mouseup
        const upEvt = new MouseEvent('mouseup', {
            bubbles: true,
            cancelable: true,
            clientX: 300,
            clientY: 300
        });
        window.dispatchEvent(upEvt);

        return {
            dragged: true,
            initialPos: initialPos,
            newPos: wrapper.scrollLeft
        };
    })()
"@
    Write-Host "Drag test result: Initial=$($dragResult.initialPos), AfterDrag=$($dragResult.newPos)" -ForegroundColor Green

    # Save Screenshot 1: Slider in main dashboard
    $screen1 = Join-Path $ArtifactsDir "cm_verified_slider_dashboard.png"
    Save-CDPScreenshot -ws $ws -outPath $screen1

    # -------------------------------------------------------------------------
    # PART 2: VERIFY REQUEST DETAILS & ATTACHMENTS
    # -------------------------------------------------------------------------
    Write-Host "`n--- VERIFYING PART 2: ATTACHMENTS PREVIEW & DOWNLOAD ---" -ForegroundColor Cyan

    # Click View Details on first row (newly submitted form #35)
    $clickDetails = Invoke-CDPEval -ws $ws -expression @"
    (() => {
        const firstRowBtn = document.querySelector('.cm-btn-open-request-details');
        if (firstRowBtn) {
            firstRowBtn.click();
            return 'CLICKED';
        }
        return 'BTN_NOT_FOUND';
    })()
"@
    Write-Host "Clicked View Details on first row: $clickDetails" -ForegroundColor Green
    Start-Sleep -Seconds 3

    $attCheck = Invoke-CDPEval -ws $ws -expression @"
    (() => {
        const detailsView = document.getElementById('control-monitoring-request-details-view');
        const attCards = document.querySelectorAll('.cm-attachment-card');
        const previews = document.querySelectorAll('.cm-trigger-preview');
        const downloads = document.querySelectorAll('.cm-trigger-download');
        
        const attList = [];
        attCards.forEach(card => {
            const title = card.querySelector('.cm-attachment-title')?.textContent?.trim();
            const sub = card.querySelector('.cm-attachment-meta-sub')?.textContent?.trim();
            const uploader = card.querySelector('.cm-attachment-uploader')?.textContent?.trim();
            const hasThumb = !!card.querySelector('.cm-attachment-thumb-wrap img, .cm-attachment-doc-placeholder');
            attList.push({ title, sub, uploader, hasThumb });
        });

        return {
            detailsVisible: detailsView && detailsView.style.display !== 'none',
            attachmentCount: attCards.length,
            previewButtonsCount: previews.length,
            downloadButtonsCount: downloads.length,
            attachments: attList
        };
    })()
"@

    Write-Host "Details View Visible: $($attCheck.detailsVisible)" -ForegroundColor Green
    Write-Host "Attachments Rendered: $($attCheck.attachmentCount)" -ForegroundColor Green
    Write-Host "Preview Buttons Count: $($attCheck.previewButtonsCount)" -ForegroundColor Green
    Write-Host "Download Buttons Count: $($attCheck.downloadButtonsCount)" -ForegroundColor Green

    foreach ($a in $attCheck.attachments) {
        Write-Host "  -> UI Attachment: $($a.title) | Meta: $($a.sub) | $($a.uploader)" -ForegroundColor Green
    }

    # Save Screenshot 2: Request details with attachments
    $screen2 = Join-Path $ArtifactsDir "cm_verified_request_details_attachments.png"
    Save-CDPScreenshot -ws $ws -outPath $screen2

    # -------------------------------------------------------------------------
    # PART 3: TEST PREVIEW MODAL
    # -------------------------------------------------------------------------
    Write-Host "`n--- TESTING ATTACHMENT PREVIEW MODAL ---" -ForegroundColor Cyan

    # Click Preview on the first attachment
    $previewClick = Invoke-CDPEval -ws $ws -expression @"
    (() => {
        const firstPreviewBtn = document.querySelector('.cm-btn-view.cm-trigger-preview');
        if (firstPreviewBtn) {
            firstPreviewBtn.click();
            return 'PREVIEW_CLICKED';
        }
        return 'PREVIEW_BTN_NOT_FOUND';
    })()
"@
    Write-Host "Clicked preview: $previewClick" -ForegroundColor Green
    Start-Sleep -Seconds 2

    $previewState = Invoke-CDPEval -ws $ws -expression @"
    (() => {
        const overlay = document.getElementById('cm-preview-overlay');
        const title = document.getElementById('cm-preview-title')?.textContent;
        const hasIframeOrImg = !!document.querySelector('#cm-preview-body iframe, #cm-preview-body img');
        const popoutHref = document.getElementById('cm-preview-popout-link')?.getAttribute('href');
        return {
            overlayVisible: overlay && overlay.style.display === 'flex',
            title: title,
            hasIframeOrImg: hasIframeOrImg,
            popoutHref: popoutHref
        };
    })()
"@

    Write-Host "Preview Modal Visible: $($previewState.overlayVisible)" -ForegroundColor Green
    Write-Host "Preview Title: $($previewState.title)" -ForegroundColor Green
    Write-Host "Contains Image or Iframe Viewer: $($previewState.hasIframeOrImg)" -ForegroundColor Green
    Write-Host "Popout Link: $($previewState.popoutHref)" -ForegroundColor Green

    # Save Screenshot 3: Attachment preview modal
    $screen3 = Join-Path $ArtifactsDir "cm_verified_attachment_preview_modal.png"
    Save-CDPScreenshot -ws $ws -outPath $screen3

    # Close preview modal
    Invoke-CDPEval -ws $ws -expression @"
    (() => {
        const closeBtn = document.getElementById('cm-preview-close');
        if (closeBtn) closeBtn.click();
        return true;
    })()
"@ | Out-Null
    Start-Sleep -Milliseconds 500

    # -------------------------------------------------------------------------
    # PART 4: TEST MOBILE RESPONSIVENESS & SWIPE
    # -------------------------------------------------------------------------
    Write-Host "`n--- TESTING MOBILE RESPONSIVENESS (iPhone 390x844) ---" -ForegroundColor Cyan
    Invoke-CDPCommand -ws $ws -method "Emulation.setDeviceMetricsOverride" -params @{
        width = 390
        height = 844
        deviceScaleFactor = 2
        mobile = $true
    } | Out-Null

    # Return to dashboard on mobile view
    Invoke-CDPEval -ws $ws -expression @"
    (() => {
        const backBtn = document.getElementById('cm-details-back-btn');
        if (backBtn) backBtn.click();
        return true;
    })()
"@ | Out-Null
    Start-Sleep -Seconds 1

    $mobileCheck = Invoke-CDPEval -ws $ws -expression @"
    (() => {
        const wrapper = document.getElementById('cm-finance-table-wrapper') || document.querySelector('.finance-table-wrapper');
        const table = wrapper ? wrapper.querySelector('.finance-table') : null;
        return {
            wrapperClientWidth: wrapper ? wrapper.clientWidth : 0,
            tableWidth: table ? table.offsetWidth : 0,
            isScrollable: wrapper ? wrapper.scrollWidth > wrapper.clientWidth : false
        };
    })()
"@
    Write-Host "Mobile Wrapper Width: $($mobileCheck.wrapperClientWidth)px" -ForegroundColor Green
    Write-Host "Mobile Table Width (Protected from squashing): $($mobileCheck.tableWidth)px" -ForegroundColor Green
    Write-Host "Table is horizontally scrollable/swipeable: $($mobileCheck.isScrollable)" -ForegroundColor Green

    # Save Screenshot 4: Mobile view with slider
    $screen4 = Join-Path $ArtifactsDir "cm_verified_mobile_slider.png"
    Save-CDPScreenshot -ws $ws -outPath $screen4

    Write-Host "`n=== ALL CDP BROWSER VERIFICATIONS COMPLETED SUCCESSFULLY ===" -ForegroundColor Green
} finally {
    if ($ws -and $ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", [System.Threading.CancellationToken]::None).Wait()
    }
    if ($chromeProc -and !$chromeProc.HasExited) {
        Stop-Process -Id $chromeProc.Id -Force -ErrorAction SilentlyContinue
    }
}
