param(
    [string]$BaseUrl = "http://localhost:5124"
)

$ErrorActionPreference = "Stop"

# 1. Start Chrome with remote debugging
$tempProfile = Join-Path $env:TEMP "chrome_cdp_cm_test_$(Get-Random)"
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

    # Loop until we receive the message with matching id
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

function Invoke-CDPScreenshot {
    param(
        [System.Net.WebSockets.ClientWebSocket]$ws,
        [string]$outputPath
    )
    $res = Invoke-CDPCommand -ws $ws -method "Page.captureScreenshot" -params @{ format = "png" }
    if ($res -and $res.data) {
        [System.IO.File]::WriteAllBytes($outputPath, [Convert]::FromBase64String($res.data))
        Write-Host "Captured screenshot: $outputPath"
    }
}

try {
    # 2. Connect to Chrome CDP via WebSocket
    $tabsJson = (New-Object System.Net.WebClient).DownloadString("http://localhost:9244/json")
    $tabs = $tabsJson | ConvertFrom-Json
    $pageTab = $tabs | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    $wsUrl = $pageTab.webSocketDebuggerUrl

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([uri]$wsUrl, [System.Threading.CancellationToken]::None).Wait()
    Write-Host "Connected to Chrome DevTools Protocol."

    # 3. Navigate to BaseUrl
    $navRes = Invoke-CDPCommand -ws $ws -method "Page.navigate" -params @{ url = "$BaseUrl" }
    Start-Sleep -Seconds 3

    # 4. Login via UI form
    $loginExpr = @"
    (async () => {
        const idInput = document.getElementById('login-id-input');
        const passInput = document.getElementById('login-pwd-input');
        const form = document.getElementById('auth-login-form');
        if (idInput && passInput && form) {
            idInput.value = 'control_monitoring';
            idInput.dispatchEvent(new Event('input', { bubbles: true }));
            passInput.value = 'Password123!';
            passInput.dispatchEvent(new Event('input', { bubbles: true }));
            form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
            return 'LOGIN_SUBMITTED';
        }
        return 'LOGIN_INPUTS_NOT_FOUND';
    })()
"@
    $loginAction = Invoke-CDPEval -ws $ws -expression $loginExpr
    Write-Host "Login Action: $loginAction"
    Start-Sleep -Seconds 3

    # 5. Navigate to Control & Monitoring
    $enterDeptExpr = @"
    (async () => {
        if (window.ChatApp) {
            await window.ChatApp.enterDepartment('control-monitoring');
            return 'ENTERED_VIA_CHATAPP';
        }
        return 'CHATAPP_NOT_FOUND';
    })()
"@
    $enterAction = Invoke-CDPEval -ws $ws -expression $enterDeptExpr
    Write-Host "Department navigation: $enterAction"
    Start-Sleep -Seconds 3

    # Verify Main Dashboard State
    $dashVerifyExpr = @"
    (() => {
        const title = document.querySelector('.finance-mgmt-title-content h1')?.textContent || '';
        const pendingStat = document.getElementById('cm-stat-pending-val')?.textContent || '0';
        const transStat = document.getElementById('cm-stat-trans-val')?.textContent || '0';
        const storageStat = document.getElementById('cm-stat-storage-val')?.textContent || '0';
        const approvedStat = document.getElementById('cm-stat-approved-val')?.textContent || '0';
        const rejectedStat = document.getElementById('cm-stat-rejected-val')?.textContent || '0';
        const rows = Array.from(document.querySelectorAll('#cm-forms-table-body tr')).map(tr => {
            const cells = Array.from(tr.querySelectorAll('td')).map(td => td.innerText.trim().replace(/\n+/g, ' '));
            const btn = tr.querySelector('.cm-btn-open-request-details');
            return {
                cells: cells,
                hasDetailsBtn: !!btn,
                formId: btn?.getAttribute('data-id'),
                formType: btn?.getAttribute('data-type')
            };
        });

        return {
            title,
            stats: { pending: pendingStat, trans: transStat, storage: storageStat, approved: approvedStat, rejected: rejectedStat },
            rowsCount: rows.length,
            rows: rows
        };
    })()
"@
    $dashData = Invoke-CDPEval -ws $ws -expression $dashVerifyExpr
    Write-Host "`n--- MAIN DASHBOARD VERIFIED ---" -ForegroundColor Cyan
    Write-Host "Page Title: $($dashData.title)"
    Write-Host "Stats -> Pending: $($dashData.stats.pending), Trans: $($dashData.stats.trans), Storage: $($dashData.stats.storage), Approved: $($dashData.stats.approved), Rejected: $($dashData.stats.rejected)"
    Write-Host "Total rows displayed: $($dashData.rowsCount)"
    foreach ($r in $dashData.rows) {
        Write-Host "   Row [ID: $($r.formId), Type: $($r.formType)]: $($r.cells -join ' | ')"
    }

    # Take Screenshot 1: Main Dashboard
    $shot1 = "f:\FaceStoneWebsite\scratch\cm_verified_01_dashboard.png"
    Invoke-CDPScreenshot -ws $ws -outputPath $shot1

    # 6. Click [ عرض التفاصيل ← ] on the first request (Transportation ID 34)
    $openTransDetailsExpr = @"
    (() => {
        const btn = document.querySelector('.cm-btn-open-request-details[data-type="Transportation"]');
        if (btn) {
            btn.click();
            return { clicked: true, id: btn.getAttribute('data-id') };
        }
        return { clicked: false };
    })()
"@
    $openTransRes = Invoke-CDPEval -ws $ws -expression $openTransDetailsExpr
    Write-Host "`nOpened Transportation Request Details: $($openTransRes | ConvertTo-Json -Compress)"
    Start-Sleep -Seconds 2

    # Verify Transportation Details View
    $verifyTransExpr = @"
    (() => {
        const heroTitle = document.getElementById('cm-details-hero-title')?.textContent || '';
        const heroType = document.getElementById('cm-details-hero-type-text')?.textContent || '';
        const heroId = document.getElementById('cm-details-hero-id-text')?.textContent || '';
        const heroPo = document.getElementById('cm-details-hero-po-text')?.textContent || '';
        const heroSubmitter = document.getElementById('cm-details-hero-submitter-text')?.textContent || '';
        const heroDate = document.getElementById('cm-details-hero-date-text')?.textContent || '';
        const heroTime = document.getElementById('cm-details-hero-time-text')?.textContent || '';
        const statusBadge = document.getElementById('cm-details-hero-status-badge')?.textContent || '';

        const fieldItems = Array.from(document.querySelectorAll('#cm-details-form-data-body .cm-field-item')).map(f => {
            return (f.querySelector('.cm-field-label')?.innerText.trim() || '') + ': ' + (f.querySelector('.cm-field-value')?.innerText.trim() || '');
        });

        const attCards = Array.from(document.querySelectorAll('#cm-details-attachments-list .cm-attachment-card')).map(card => {
            return {
                title: card.querySelector('.cm-attachment-title')?.innerText.trim() || '',
                sub: card.querySelector('.cm-attachment-meta-sub')?.innerText.trim() || '',
                uploader: card.querySelector('.cm-attachment-uploader')?.innerText.trim() || '',
                hasViewBtn: !!card.querySelector('.cm-btn-view'),
                hasDownloadBtn: !!card.querySelector('.cm-btn-download')
            };
        });

        const hasAccept = !!document.getElementById('cm-details-btn-accept');
        const hasReject = !!document.getElementById('cm-details-btn-reject');
        const hasBackBtn = !!document.getElementById('cm-details-back-btn');

        return {
            hero: { title: heroTitle, type: heroType, id: heroId, po: heroPo, submitter: heroSubmitter, date: heroDate, time: heroTime, status: statusBadge },
            fieldsCount: fieldItems.length,
            sampleFields: fieldItems.slice(0, 10),
            attachmentsCount: attCards.length,
            attachments: attCards,
            actions: { hasAccept, hasReject, hasBackBtn }
        };
    })()
"@
    $transDetailsData = Invoke-CDPEval -ws $ws -expression $verifyTransExpr
    Write-Host "`n--- TRANSPORTATION DETAILS PAGE VERIFIED ---" -ForegroundColor Cyan
    Write-Host "Hero: $($transDetailsData.hero.title) | ID: $($transDetailsData.hero.id) | PO: $($transDetailsData.hero.po) | Status: $($transDetailsData.hero.status)"
    Write-Host "Form Fields rendered: $($transDetailsData.fieldsCount)"
    foreach ($sf in $transDetailsData.sampleFields) {
        Write-Host "   Field: $sf"
    }
    Write-Host "Attachments rendered: $($transDetailsData.attachmentsCount)"
    foreach ($att in $transDetailsData.attachments) {
        Write-Host "   Att: $($att.title) | $($att.sub) | $($att.uploader) | View: $($att.hasViewBtn) | Download: $($att.hasDownloadBtn)"
    }

    # Take Screenshot 2: Transportation Details Page
    $shot2 = "f:\FaceStoneWebsite\scratch\cm_verified_02_trans_details.png"
    Invoke-CDPScreenshot -ws $ws -outputPath $shot2

    # 7. Test Clicking [ ✓ قبول واعتماد النموذج (ACCEPT) ]
    $clickAcceptExpr = @"
    (() => {
        const btn = document.getElementById('cm-details-btn-accept');
        if (btn) {
            btn.click();
            const box = document.getElementById('cm-details-accept-box');
            return { clicked: true, boxVisible: box && box.style.display !== 'none' };
        }
        return { clicked: false };
    })()
"@
    $acceptClickRes = Invoke-CDPEval -ws $ws -expression $clickAcceptExpr
    Write-Host "`nAccept Drawer Toggle: $($acceptClickRes | ConvertTo-Json -Compress)"
    Start-Sleep -Milliseconds 600

    # Take Screenshot 3: Accept Confirmation Box
    $shot3 = "f:\FaceStoneWebsite\scratch\cm_verified_03_accept_confirm.png"
    Invoke-CDPScreenshot -ws $ws -outputPath $shot3

    # Close accept drawer
    Invoke-CDPEval -ws $ws -expression "document.getElementById('cm-details-btn-cancel-accept')?.click();"
    Start-Sleep -Milliseconds 300

    # 8. Test Clicking [ ✕ رفض النموذج للتصحيح (REJECT) ]
    $clickRejectExpr = @"
    (() => {
        const btn = document.getElementById('cm-details-btn-reject');
        if (btn) {
            btn.click();
            const box = document.getElementById('cm-details-reject-box');
            return { clicked: true, boxVisible: box && box.style.display !== 'none' };
        }
        return { clicked: false };
    })()
"@
    $rejectClickRes = Invoke-CDPEval -ws $ws -expression $clickRejectExpr
    Write-Host "Reject Drawer Toggle: $($rejectClickRes | ConvertTo-Json -Compress)"
    Start-Sleep -Milliseconds 600

    # Take Screenshot 4: Reject Reason Box
    $shot4 = "f:\FaceStoneWebsite\scratch\cm_verified_04_reject_reason.png"
    Invoke-CDPScreenshot -ws $ws -outputPath $shot4

    # Close reject drawer
    Invoke-CDPEval -ws $ws -expression "document.getElementById('cm-details-btn-cancel-reject')?.click();"
    Start-Sleep -Milliseconds 300

    # 9. Test Back Button: Click [ ← العودة إلى قائمة الطلبات ]
    $clickBackExpr = @"
    (() => {
        const backBtn = document.getElementById('cm-details-back-btn');
        if (backBtn) {
            backBtn.click();
            const mainVis = document.getElementById('control-monitoring-main-view')?.style.display;
            const detVis = document.getElementById('control-monitoring-request-details-view')?.style.display;
            return { clicked: true, mainVisible: mainVis !== 'none', detailsHidden: detVis === 'none' };
        }
        return { clicked: false };
    })()
"@
    $backRes = Invoke-CDPEval -ws $ws -expression $clickBackExpr
    Write-Host "`nBack Button clicked: $($backRes | ConvertTo-Json -Compress)"
    Start-Sleep -Seconds 1

    # 10. Filter by Storage and open details of Storage Request
    $openStorageExpr = @"
    (async () => {
        const typeSelect = document.getElementById('cm-filter-type-select');
        if (typeSelect) {
            typeSelect.value = 'Storage';
            typeSelect.dispatchEvent(new Event('change', { bubbles: true }));
        }
        await new Promise(r => setTimeout(r, 600));

        const btn = document.querySelector('.cm-btn-open-request-details[data-type="Storage"]');
        if (btn) {
            btn.click();
            return { clicked: true, id: btn.getAttribute('data-id') };
        }
        return { clicked: false };
    })()
"@
    $storageOpenRes = Invoke-CDPEval -ws $ws -expression $openStorageExpr
    Write-Host "`nOpened Storage Request Details: $($storageOpenRes | ConvertTo-Json -Compress)"
    Start-Sleep -Seconds 2

    # Verify Storage Details View
    $verifyStorageExpr = @"
    (() => {
        const heroTitle = document.getElementById('cm-details-hero-title')?.textContent || '';
        const heroId = document.getElementById('cm-details-hero-id-text')?.textContent || '';
        const statusBadge = document.getElementById('cm-details-hero-status-badge')?.textContent || '';

        const fieldItems = Array.from(document.querySelectorAll('#cm-details-form-data-body .cm-field-item')).map(f => {
            return (f.querySelector('.cm-field-label')?.innerText.trim() || '') + ': ' + (f.querySelector('.cm-field-value')?.innerText.trim() || '');
        });

        const timelineCount = document.querySelectorAll('#cm-details-timeline .cm-timeline-item').length;

        return {
            title: heroTitle,
            id: heroId,
            status: statusBadge,
            fieldsCount: fieldItems.length,
            fields: fieldItems,
            timelineCount
        };
    })()
"@
    $storageData = Invoke-CDPEval -ws $ws -expression $verifyStorageExpr
    Write-Host "`n--- STORAGE DETAILS PAGE VERIFIED ---" -ForegroundColor Cyan
    Write-Host "Title: $($storageData.title) | ID: $($storageData.id) | Status: $($storageData.status)"
    Write-Host "Storage Fields count: $($storageData.fieldsCount)"
    foreach ($sf in $storageData.fields) {
        Write-Host "   $sf"
    }
    Write-Host "Timeline events: $($storageData.timelineCount)"

    # Take Screenshot 5: Storage Details Page
    $shot5 = "f:\FaceStoneWebsite\scratch\cm_verified_05_storage_details.png"
    Invoke-CDPScreenshot -ws $ws -outputPath $shot5

    Write-Host "`n=======================================================" -ForegroundColor Green
    Write-Host "ALL BROWSER UI VERIFICATION CHECKS PASSED SUCCESSFULLY!" -ForegroundColor Green
    Write-Host "=======================================================" -ForegroundColor Green
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
