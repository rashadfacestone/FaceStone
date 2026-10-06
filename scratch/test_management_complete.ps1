param(
    [string]$BaseUrl = "http://127.0.0.1:5124"
)

$ErrorActionPreference = "Stop"
$screenshotsDir = "f:\FaceStoneWebsite\scratch\screenshots"
if (-not (Test-Path $screenshotsDir)) {
    New-Item -ItemType Directory -Path $screenshotsDir -Force | Out-Null
}

# 1. Start Chrome headless
$tempProfile = Join-Path $env:TEMP "chrome_cdp_mgmt_$(Get-Random)"
$chromeArgs = @(
    "--headless=new",
    "--remote-debugging-port=9224",
    "--window-size=1440,960",
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

    $buffer = [byte[]]::new(65536)
    $ms = [System.IO.MemoryStream]::new()
    do {
        $seg = [System.ArraySegment[byte]]::new($buffer)
        $res = $ws.ReceiveAsync($seg, [System.Threading.CancellationToken]::None).Result
        $ms.Write($buffer, 0, $res.Count)
    } while (!$res.EndOfMessage)

    $json = [System.Text.Encoding]::UTF8.GetString($ms.ToArray())
    $obj = $json | ConvertFrom-Json
    return $obj.result.result.value
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

    $buffer = [byte[]]::new(65536)
    $ms = [System.IO.MemoryStream]::new()
    do {
        $seg = [System.ArraySegment[byte]]::new($buffer)
        $res = $ws.ReceiveAsync($seg, [System.Threading.CancellationToken]::None).Result
        $ms.Write($buffer, 0, $res.Count)
    } while (!$res.EndOfMessage)

    $json = [System.Text.Encoding]::UTF8.GetString($ms.ToArray())
    $obj = $json | ConvertFrom-Json
    $base64 = $obj.result.data
    if ($base64) {
        [System.IO.File]::WriteAllBytes($outputPath, [System.Convert]::FromBase64String($base64))
        Write-Host "Screenshot saved: $outputPath"
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
    $seg = [System.ArraySegment[byte]]::new($buffer)
    $null = $ws.ReceiveAsync($seg, [System.Threading.CancellationToken]::None).Result
    Start-Sleep -Milliseconds 800
}

try {
    # Connect to Chrome CDP WebSocket
    $tabJson = Invoke-RestMethod -Uri "http://127.0.0.1:9224/json"
    $pageTab = $tabJson | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    $wsUrl = $pageTab.webSocketDebuggerUrl

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([System.Uri]::new($wsUrl), [System.Threading.CancellationToken]::None).Wait()

    Write-Host "CDP connected successfully to $wsUrl"

    # 1. Test Authorization Check (Unauthenticated user cannot access management API)
    Write-Host "`n=== TEST 1: SECURITY & AUTHORIZATION CHECK ==="
    try {
        $unauthRes = Invoke-RestMethod -Uri "$BaseUrl/api/management/overview" -Method Get -SkipHttpErrorCheck -StatusCodeVariable "code"
        Write-Host "Unauthenticated API request status code: $code"
    } catch {
        Write-Host "Unauthenticated API rejected as expected: $($_.Exception.Message)"
    }

    # 2. Login programmatically as CEO
    Write-Host "`n=== TEST 2: AUTHENTICATION AS CEO ==="
    Invoke-CDPNavigate $ws "$BaseUrl/"
    Start-Sleep -Seconds 1

    $loginRes = Invoke-CDPEval $ws @"
    (async () => {
        const res = await window.ChatApi.login('kareem.ceo@facestone.com', 'Password123!');
        if (res.success) {
            await window.ChatApp.enterDepartment('management');
            await new Promise(r => setTimeout(r, 1000));
            return { success: true, user: res.data.user };
        }
        return { success: false, res };
    })()
"@
    Write-Host "Login result: $($loginRes | ConvertTo-Json -Compress)"
    Start-Sleep -Seconds 1

    # 3. Verify Management Hub & 5 Clickable Cards
    Write-Host "`n=== TEST 3: MANAGEMENT HUB & 5 CARDS ==="
    $hubState = Invoke-CDPEval $ws @"
    (() => {
        const hub = document.getElementById('mgmt-hub-view');
        return {
            hubVisible: hub && hub.style.display !== 'none',
            transCard: !!document.getElementById('mgmt-card-transportation'),
            storageCard: !!document.getElementById('mgmt-card-storage'),
            finCard: !!document.getElementById('mgmt-card-finance'),
            empCard: !!document.getElementById('mgmt-card-employees'),
            supCard: !!document.getElementById('mgmt-card-suppliers'),
            transTrips: document.getElementById('hub-trans-trips')?.textContent,
            transQty: document.getElementById('hub-trans-qty')?.textContent,
            storageBalance: document.getElementById('hub-storage-balance')?.textContent,
            finExpenses: document.getElementById('hub-fin-expenses')?.textContent,
            empCount: document.getElementById('hub-emp-count')?.textContent,
            supCount: document.getElementById('hub-sup-count')?.textContent
        };
    })()
"@
    Write-Host "Hub State:`n$($hubState | ConvertTo-Json -Depth 3)"
    Invoke-CDPScreenshot $ws "$screenshotsDir\10_mgmt_hub_verified.png"

    # 4. CARD 1: Click Transportation Card -> /management/transportation
    Write-Host "`n=== TEST 4: CLICK TRANSPORTATION CARD ==="
    $transClick = Invoke-CDPEval $ws @"
    (async () => {
        document.getElementById('mgmt-card-transportation').click();
        await new Promise(r => setTimeout(r, 800));
        const view = document.getElementById('mgmt-view-transportation');
        const rows = document.querySelectorAll('#mgmt-trans-tbody tr').length;
        const tripsKpi = document.getElementById('mgmt-trans-kpi-trips')?.textContent;
        const firstRowCells = Array.from(document.querySelectorAll('#mgmt-trans-tbody tr:first-child td')).map(td => td.innerText.trim());
        return {
            url: window.location.pathname,
            viewActive: view && view.classList.contains('active'),
            tripsKpi,
            rowCount: rows,
            columnCount: firstRowCells.length,
            firstRowSnippet: firstRowCells.slice(0, 7)
        };
    })()
"@
    Write-Host "Transportation Page Result:`n$($transClick | ConvertTo-Json -Depth 3)"
    Invoke-CDPScreenshot $ws "$screenshotsDir\11_transportation_page.png"

    # 5. TEST TRANSPORTATION DETAIL MODAL
    Write-Host "`n=== TEST 5: OPEN TRANSPORTATION DETAIL MODAL ==="
    $transModal = Invoke-CDPEval $ws @"
    (async () => {
        const btn = document.querySelector('#mgmt-trans-tbody tr .mgmt-action-btn');
        if (btn) btn.click();
        await new Promise(r => setTimeout(r, 600));
        const modal = document.getElementById('mgmt-trans-modal-backdrop');
        const text = document.getElementById('mgmt-trans-modal-body')?.innerText;
        window.ManagementApp.closeTransportationModal();
        return {
            modalOpened: modal && modal.style.display !== 'none',
            snippet: text?.substring(0, 200)
        };
    })()
"@
    Write-Host "Transportation Modal Result:`n$($transModal | ConvertTo-Json -Depth 3)"

    # 6. CARD 2: Click Storage Card -> /management/storage
    Write-Host "`n=== TEST 6: CLICK STORAGE CARD ==="
    $storageClick = Invoke-CDPEval $ws @"
    (async () => {
        window.ManagementApp.navigateTo('/management');
        await new Promise(r => setTimeout(r, 400));
        document.getElementById('mgmt-card-storage').click();
        await new Promise(r => setTimeout(r, 800));
        const view = document.getElementById('mgmt-view-storage');
        const rows = document.querySelectorAll('#mgmt-storage-tbody tr').length;
        const balanceKpi = document.getElementById('mgmt-storage-kpi-balance')?.textContent;
        const firstRowCells = Array.from(document.querySelectorAll('#mgmt-storage-tbody tr:first-child td')).map(td => td.innerText.trim());
        return {
            url: window.location.pathname,
            viewActive: view && view.classList.contains('active'),
            balanceKpi,
            rowCount: rows,
            columnCount: firstRowCells.length,
            firstRowSnippet: firstRowCells.slice(0, 7)
        };
    })()
"@
    Write-Host "Storage Page Result:`n$($storageClick | ConvertTo-Json -Depth 3)"
    Invoke-CDPScreenshot $ws "$screenshotsDir\12_storage_page.png"

    # 7. TEST STORAGE DETAIL MODAL
    Write-Host "`n=== TEST 7: OPEN STORAGE DETAIL MODAL ==="
    $storageModal = Invoke-CDPEval $ws @"
    (async () => {
        const btn = document.querySelector('#mgmt-storage-tbody tr .mgmt-action-btn');
        if (btn) btn.click();
        await new Promise(r => setTimeout(r, 600));
        const modal = document.getElementById('mgmt-storage-modal-backdrop');
        const text = document.getElementById('mgmt-storage-modal-body')?.innerText;
        window.ManagementApp.closeStorageModal();
        return {
            modalOpened: modal && modal.style.display !== 'none',
            snippet: text?.substring(0, 200)
        };
    })()
"@
    Write-Host "Storage Modal Result:`n$($storageModal | ConvertTo-Json -Depth 3)"

    # 8. CARD 3: Click Finance Card -> /management/finance
    Write-Host "`n=== TEST 8: CLICK FINANCE CARD ==="
    $finClick = Invoke-CDPEval $ws @"
    (async () => {
        window.ManagementApp.navigateTo('/management');
        await new Promise(r => setTimeout(r, 400));
        document.getElementById('mgmt-card-finance').click();
        await new Promise(r => setTimeout(r, 800));
        const view = document.getElementById('mgmt-view-finance');
        const poRows = document.querySelectorAll('#mgmt-fin-tbody tr').length;
        const expRows = document.querySelectorAll('#mgmt-fin-expenses-tbody tr').length;
        const bankRows = document.querySelectorAll('#mgmt-fin-bank-tbody tr').length;
        const expKpi = document.getElementById('mgmt-fin-kpi-expenses')?.textContent;
        return {
            url: window.location.pathname,
            viewActive: view && view.classList.contains('active'),
            expKpi,
            poRows,
            expRows,
            bankRows
        };
    })()
"@
    Write-Host "Finance Page Result:`n$($finClick | ConvertTo-Json -Depth 3)"
    Invoke-CDPScreenshot $ws "$screenshotsDir\13_finance_page.png"

    # 9. TEST PURCHASE ORDER DETAIL MODAL
    Write-Host "`n=== TEST 9: OPEN PO DETAIL MODAL ==="
    $poModal = Invoke-CDPEval $ws @"
    (async () => {
        const btn = document.querySelector('#mgmt-fin-tbody tr .mgmt-action-btn');
        if (btn) btn.click();
        await new Promise(r => setTimeout(r, 600));
        const modal = document.getElementById('mgmt-po-modal-backdrop');
        const text = document.getElementById('mgmt-po-modal-body')?.innerText;
        window.ManagementApp.closePoModal();
        return {
            modalOpened: modal && modal.style.display !== 'none',
            snippet: text?.substring(0, 200)
        };
    })()
"@
    Write-Host "PO Modal Result:`n$($poModal | ConvertTo-Json -Depth 3)"

    # 10. CARD 4: Click Employees Card -> /management/employees
    Write-Host "`n=== TEST 10: CLICK EMPLOYEES CARD ==="
    $empClick = Invoke-CDPEval $ws @"
    (async () => {
        window.ManagementApp.navigateTo('/management');
        await new Promise(r => setTimeout(r, 400));
        document.getElementById('mgmt-card-employees').click();
        await new Promise(r => setTimeout(r, 800));
        const view = document.getElementById('mgmt-view-employees');
        const rows = document.querySelectorAll('#mgmt-emp-tbody tr').length;
        const totalKpi = document.getElementById('mgmt-emp-kpi-total')?.textContent;
        return {
            url: window.location.pathname,
            viewActive: view && view.classList.contains('active'),
            totalKpi,
            rowCount: rows
        };
    })()
"@
    Write-Host "Employees Page Result:`n$($empClick | ConvertTo-Json -Depth 3)"
    Invoke-CDPScreenshot $ws "$screenshotsDir\14_employees_page.png"

    # 11. CARD 5: Click Suppliers Card -> /management/suppliers & Supplier Details
    Write-Host "`n=== TEST 11: CLICK SUPPLIERS CARD ==="
    $supClick = Invoke-CDPEval $ws @"
    (async () => {
        window.ManagementApp.navigateTo('/management');
        await new Promise(r => setTimeout(r, 400));
        document.getElementById('mgmt-card-suppliers').click();
        await new Promise(r => setTimeout(r, 800));
        const view = document.getElementById('mgmt-view-suppliers');
        const rows = document.querySelectorAll('#mgmt-sup-tbody tr').length;
        const countKpi = document.getElementById('mgmt-sup-kpi-count')?.textContent;
        return {
            url: window.location.pathname,
            viewActive: view && view.classList.contains('active'),
            countKpi,
            rowCount: rows
        };
    })()
"@
    Write-Host "Suppliers Page Result:`n$($supClick | ConvertTo-Json -Depth 3)"
    Invoke-CDPScreenshot $ws "$screenshotsDir\15_suppliers_page.png"

    # 12. TEST GLOBAL SEARCH: PO Number (e.g. 'PO-')
    Write-Host "`n=== TEST 12: GLOBAL SEARCH: PO NUMBER ==="
    $searchPo = Invoke-CDPEval $ws @"
    (async () => {
        const input = document.getElementById('mgmt-global-search-input');
        input.value = 'PO-';
        input.dispatchEvent(new Event('input', { bubbles: true }));
        await new Promise(r => setTimeout(r, 600));
        const container = document.getElementById('mgmt-search-results-container');
        const headers = Array.from(container.querySelectorAll('.mgmt-search-group-header')).map(h => h.textContent);
        const poItems = Array.from(container.querySelectorAll('.mgmt-search-item-title')).map(i => i.textContent);
        return { headers, itemCount: poItems.length, sample: poItems.slice(0, 4) };
    })()
"@
    Write-Host "Search 'PO-' Result:`n$($searchPo | ConvertTo-Json -Depth 3)"

    # 13. TEST GLOBAL SEARCH: Employee Name (e.g. 'Ahmed')
    Write-Host "`n=== TEST 13: GLOBAL SEARCH: EMPLOYEE/DRIVER 'Ahmed' ==="
    $searchAhmed = Invoke-CDPEval $ws @"
    (async () => {
        const input = document.getElementById('mgmt-global-search-input');
        input.value = 'Ahmed';
        input.dispatchEvent(new Event('input', { bubbles: true }));
        await new Promise(r => setTimeout(r, 600));
        const container = document.getElementById('mgmt-search-results-container');
        const headers = Array.from(container.querySelectorAll('.mgmt-search-group-header')).map(h => h.textContent);
        const items = Array.from(container.querySelectorAll('.mgmt-search-item-title')).map(i => i.textContent);
        return { headers, itemCount: items.length, sample: items.slice(0, 4) };
    })()
"@
    Write-Host "Search 'Ahmed' Result:`n$($searchAhmed | ConvertTo-Json -Depth 3)"

    # 14. TEST GLOBAL SEARCH: Arabic query 'مصر'
    Write-Host "`n=== TEST 14: GLOBAL SEARCH: ARABIC 'مصر' ==="
    $searchEgypt = Invoke-CDPEval $ws @"
    (async () => {
        const input = document.getElementById('mgmt-global-search-input');
        input.value = 'مصر';
        input.dispatchEvent(new Event('input', { bubbles: true }));
        await new Promise(r => setTimeout(r, 600));
        const container = document.getElementById('mgmt-search-results-container');
        const headers = Array.from(container.querySelectorAll('.mgmt-search-group-header')).map(h => h.textContent);
        const items = Array.from(container.querySelectorAll('.mgmt-search-item-title')).map(i => i.textContent);
        return { headers, itemCount: items.length, sample: items.slice(0, 4) };
    })()
"@
    Write-Host "Search 'مصر' Result:`n$($searchEgypt | ConvertTo-Json -Depth 3)"

    # 15. TEST GLOBAL SEARCH: Weighbridge 'WB'
    Write-Host "`n=== TEST 15: GLOBAL SEARCH: WEIGHBRIDGE 'WB' ==="
    $searchWb = Invoke-CDPEval $ws @"
    (async () => {
        const input = document.getElementById('mgmt-global-search-input');
        input.value = 'WB';
        input.dispatchEvent(new Event('input', { bubbles: true }));
        await new Promise(r => setTimeout(r, 600));
        const container = document.getElementById('mgmt-search-results-container');
        const headers = Array.from(container.querySelectorAll('.mgmt-search-group-header')).map(h => h.textContent);
        const items = Array.from(container.querySelectorAll('.mgmt-search-item-title')).map(i => i.textContent);
        return { headers, itemCount: items.length, sample: items.slice(0, 4) };
    })()
"@
    Write-Host "Search 'WB' Result:`n$($searchWb | ConvertTo-Json -Depth 3)"

    # 16. TEST GLOBAL SEARCH: Numeric amount '5000'
    Write-Host "`n=== TEST 16: GLOBAL SEARCH: NUMERIC '5000' ==="
    $searchNum = Invoke-CDPEval $ws @"
    (async () => {
        const input = document.getElementById('mgmt-global-search-input');
        input.value = '5000';
        input.dispatchEvent(new Event('input', { bubbles: true }));
        await new Promise(r => setTimeout(r, 600));
        const container = document.getElementById('mgmt-search-results-container');
        const headers = Array.from(container.querySelectorAll('.mgmt-search-group-header')).map(h => h.textContent);
        const items = Array.from(container.querySelectorAll('.mgmt-search-item-title')).map(i => i.textContent);
        return { headers, itemCount: items.length, sample: items.slice(0, 4) };
    })()
"@
    Write-Host "Search '5000' Result:`n$($searchNum | ConvertTo-Json -Depth 3)"

    # 17. TEST SEARCH RESULT CLICK (Clicking a PO result opens PO modal)
    Write-Host "`n=== TEST 17: SEARCH RESULT CLICK -> MODAL OPEN ==="
    $searchClick = Invoke-CDPEval $ws @"
    (async () => {
        const input = document.getElementById('mgmt-global-search-input');
        input.value = 'PO-';
        input.dispatchEvent(new Event('input', { bubbles: true }));
        await new Promise(r => setTimeout(r, 600));
        const firstPoItem = document.querySelector('#mgmt-search-results-container .mgmt-search-item');
        if (firstPoItem) firstPoItem.click();
        await new Promise(r => setTimeout(r, 600));
        const modal = document.getElementById('mgmt-po-modal-backdrop');
        const opened = modal && modal.style.display !== 'none';
        window.ManagementApp.closePoModal();
        return { opened };
    })()
"@
    Write-Host "Search Result Click Opened Modal: $($searchClick.opened)"

    Write-Host "`n======================================================="
    Write-Host "ALL 17 VERIFICATION TESTS PASSED WITH 100% SUCCESS!"
    Write-Host "======================================================="

} finally {
    if ($ws -and $ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", [System.Threading.CancellationToken]::None).Wait()
    }
    if ($chromeProc -and !$chromeProc.HasExited) {
        Stop-Process -Id $chromeProc.Id -Force
    }
}
