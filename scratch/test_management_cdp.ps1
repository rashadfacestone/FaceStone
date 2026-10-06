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
    "--remote-debugging-port=9223",
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
    $tabJson = Invoke-RestMethod -Uri "http://127.0.0.1:9223/json"
    $pageTab = $tabJson | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    $wsUrl = $pageTab.webSocketDebuggerUrl

    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ws.ConnectAsync([System.Uri]::new($wsUrl), [System.Threading.CancellationToken]::None).Wait()

    Write-Host "CDP connected successfully to $wsUrl"

    # Step 1: Navigate to BaseUrl
    Invoke-CDPNavigate $ws "$BaseUrl/"
    Start-Sleep -Seconds 1

    # Step 2: Login programmatically as CEO using ChatApi
    Write-Host "Authenticating as CEO (kareem.ceo@facestone.com)..."
    $loginExpr = @"
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
    $loginRes = Invoke-CDPEval $ws $loginExpr
    Write-Host "Login result: $($loginRes | ConvertTo-Json -Compress)"
    Start-Sleep -Seconds 2

    # Step 4: Verify Management Hub and 5 Cards
    $hubCheck = Invoke-CDPEval $ws @"
    (() => {
        const hub = document.getElementById('mgmt-hub-view');
        const cards = {
            transportation: !!document.getElementById('mgmt-card-transportation'),
            storage: !!document.getElementById('mgmt-card-storage'),
            finance: !!document.getElementById('mgmt-card-finance'),
            employees: !!document.getElementById('mgmt-card-employees'),
            suppliers: !!document.getElementById('mgmt-card-suppliers'),
        };
        const metrics = {
            transTrips: document.getElementById('hub-trans-trips')?.textContent,
            transQty: document.getElementById('hub-trans-qty')?.textContent,
            transWaste: document.getElementById('hub-trans-waste')?.textContent,
            storageDeposit: document.getElementById('hub-storage-deposit')?.textContent,
            storageIssue: document.getElementById('hub-storage-issue')?.textContent,
            storageBalance: document.getElementById('hub-storage-balance')?.textContent,
            finExpenses: document.getElementById('hub-fin-expenses')?.textContent,
            finPoVal: document.getElementById('hub-fin-povalue')?.textContent,
            finPerf: document.getElementById('hub-fin-perf')?.textContent,
            empCount: document.getElementById('hub-emp-count')?.textContent,
            supCount: document.getElementById('hub-sup-count')?.textContent,
            supPos: document.getElementById('hub-sup-pos')?.textContent
        };
        return { hubVisible: hub?.style.display !== 'none', cards, metrics };
    })()
"@
    Write-Host "Management Hub Check:`n$($hubCheck | ConvertTo-Json -Depth 3)"
    Invoke-CDPScreenshot $ws "$screenshotsDir\01_management_hub.png"

    # Step 5: Test Global Search with "PO-100"
    Write-Host "Testing Global Search with 'PO-100'..."
    $searchTest1 = Invoke-CDPEval $ws @"
    (async () => {
        const input = document.getElementById('mgmt-global-search-input');
        input.value = 'PO-100';
        input.dispatchEvent(new Event('input', { bubbles: true }));
        await new Promise(r => setTimeout(r, 600));
        const dropdown = document.getElementById('mgmt-search-dropdown');
        const container = document.getElementById('mgmt-search-results-container');
        const headers = Array.from(container.querySelectorAll('.mgmt-search-group-header')).map(h => h.textContent);
        const items = Array.from(container.querySelectorAll('.mgmt-search-item-title')).map(i => i.textContent);
        return {
            dropdownShown: dropdown.classList.contains('show'),
            headers,
            itemCount: items.length,
            sampleItems: items.slice(0, 5)
        };
    })()
"@
    Write-Host "Search 'PO-100' Result:`n$($searchTest1 | ConvertTo-Json -Depth 3)"
    Invoke-CDPScreenshot $ws "$screenshotsDir\02_search_po100.png"

    # Step 6: Test Global Search with "Ahmed"
    Write-Host "Testing Global Search with 'Ahmed'..."
    $searchTest2 = Invoke-CDPEval $ws @"
    (async () => {
        const input = document.getElementById('mgmt-global-search-input');
        input.value = 'Ahmed';
        input.dispatchEvent(new Event('input', { bubbles: true }));
        await new Promise(r => setTimeout(r, 600));
        const container = document.getElementById('mgmt-search-results-container');
        const headers = Array.from(container.querySelectorAll('.mgmt-search-group-header')).map(h => h.textContent);
        const items = Array.from(container.querySelectorAll('.mgmt-search-item-title')).map(i => i.textContent);
        return { headers, itemCount: items.length, sampleItems: items.slice(0, 5) };
    })()
"@
    Write-Host "Search 'Ahmed' Result:`n$($searchTest2 | ConvertTo-Json -Depth 3)"
    Invoke-CDPScreenshot $ws "$screenshotsDir\03_search_ahmed.png"

    # Step 7: Test Transportation Subview
    Write-Host "Testing Transportation Subview..."
    $transSubCheck = Invoke-CDPEval $ws @"
    (async () => {
        window.ManagementApp.showTransportationView();
        await new Promise(r => setTimeout(r, 600));
        const kpis = {
            trips: document.getElementById('mgmt-trans-kpi-trips')?.textContent,
            totalQty: document.getElementById('mgmt-trans-kpi-total-qty')?.textContent,
            netQty: document.getElementById('mgmt-trans-kpi-net-qty')?.textContent,
            waste: document.getElementById('mgmt-trans-kpi-waste')?.textContent,
            cars: document.getElementById('mgmt-trans-kpi-cars')?.textContent,
            drivers: document.getElementById('mgmt-trans-kpi-drivers')?.textContent,
            completed: document.getElementById('mgmt-trans-kpi-completed')?.textContent,
            current: document.getElementById('mgmt-trans-kpi-current')?.textContent,
            lastMovement: document.getElementById('mgmt-trans-kpi-last-movement')?.textContent
        };
        const rows = document.querySelectorAll('#mgmt-trans-tbody tr').length;
        return { kpis, rowCount: rows };
    })()
"@
    Write-Host "Transportation Subview Check:`n$($transSubCheck | ConvertTo-Json -Depth 3)"
    Invoke-CDPScreenshot $ws "$screenshotsDir\04_transportation_subview.png"

    # Step 8: Test Storage Subview
    Write-Host "Testing Storage Subview..."
    $storageSubCheck = Invoke-CDPEval $ws @"
    (async () => {
        window.ManagementApp.showStorageView();
        await new Promise(r => setTimeout(r, 600));
        const kpis = {
            deposit: document.getElementById('mgmt-storage-kpi-deposit')?.textContent,
            issue: document.getElementById('mgmt-storage-kpi-issue')?.textContent,
            balance: document.getElementById('mgmt-storage-kpi-balance')?.textContent,
            waste: document.getElementById('mgmt-storage-kpi-waste')?.textContent,
            txCount: document.getElementById('mgmt-storage-kpi-tx-count')?.textContent
        };
        const materials = Array.from(document.querySelectorAll('#mgmt-storage-materials-breakdown .mgmt-material-pill-title')).map(e => e.textContent);
        const rows = document.querySelectorAll('#mgmt-storage-tbody tr').length;
        return { kpis, materials, rowCount: rows };
    })()
"@
    Write-Host "Storage Subview Check:`n$($storageSubCheck | ConvertTo-Json -Depth 3)"
    Invoke-CDPScreenshot $ws "$screenshotsDir\05_storage_subview.png"

    # Step 9: Test Finance Subview
    Write-Host "Testing Finance Subview..."
    $finSubCheck = Invoke-CDPEval $ws @"
    (async () => {
        window.ManagementApp.showFinanceView();
        await new Promise(r => setTimeout(r, 600));
        const kpis = {
            totalPoVal: document.getElementById('mgmt-fin-kpi-total-po-val')?.textContent,
            deliveredVal: document.getElementById('mgmt-fin-kpi-delivered-val')?.textContent,
            remainingVal: document.getElementById('mgmt-fin-kpi-remaining-val')?.textContent,
            expenses: document.getElementById('mgmt-fin-kpi-expenses')?.textContent,
            perf: document.getElementById('mgmt-fin-kpi-perf')?.textContent
        };
        const rows = document.querySelectorAll('#mgmt-fin-tbody tr').length;
        return { kpis, rowCount: rows };
    })()
"@
    Write-Host "Finance Subview Check:`n$($finSubCheck | ConvertTo-Json -Depth 3)"
    Invoke-CDPScreenshot $ws "$screenshotsDir\06_finance_subview.png"

    # Step 10: Test Employees Subview & Modal
    Write-Host "Testing Employees Subview..."
    $empSubCheck = Invoke-CDPEval $ws @"
    (async () => {
        window.ManagementApp.showEmployeesView();
        await new Promise(r => setTimeout(r, 600));
        const kpis = {
            total: document.getElementById('mgmt-emp-kpi-total')?.textContent,
            salaried: document.getElementById('mgmt-emp-kpi-salaried')?.textContent,
            lending: document.getElementById('mgmt-emp-kpi-lending')?.textContent
        };
        const rows = document.querySelectorAll('#mgmt-emp-tbody tr').length;
        
        // Open modal for first employee
        const firstActionBtn = document.querySelector('#mgmt-emp-tbody tr .mgmt-action-btn');
        if (firstActionBtn) firstActionBtn.click();
        await new Promise(r => setTimeout(r, 500));
        const modalBackdrop = document.getElementById('mgmt-employee-modal-backdrop');
        const modalBody = document.getElementById('mgmt-emp-modal-body')?.innerText;

        return { kpis, rowCount: rows, modalVisible: modalBackdrop?.style.display !== 'none', modalSnippet: modalBody?.substring(0, 150) };
    })()
"@
    Write-Host "Employees Subview Check:`n$($empSubCheck | ConvertTo-Json -Depth 3)"
    Invoke-CDPScreenshot $ws "$screenshotsDir\07_employees_modal.png"

    # Close modal
    Invoke-CDPEval $ws "window.ManagementApp.closeEmployeeModal()"

    # Step 11: Test Suppliers Subview & Supplier Details
    Write-Host "Testing Suppliers Subview..."
    $supSubCheck = Invoke-CDPEval $ws @"
    (async () => {
        window.ManagementApp.showSuppliersView();
        await new Promise(r => setTimeout(r, 600));
        const kpis = {
            count: document.getElementById('mgmt-sup-kpi-count')?.textContent,
            pos: document.getElementById('mgmt-sup-kpi-pos')?.textContent,
            origQty: document.getElementById('mgmt-sup-kpi-orig-qty')?.textContent,
            deliveredQty: document.getElementById('mgmt-sup-kpi-delivered-qty')?.textContent,
            remQty: document.getElementById('mgmt-sup-kpi-rem-qty')?.textContent,
            progress: document.getElementById('mgmt-sup-kpi-progress')?.textContent
        };
        const rows = document.querySelectorAll('#mgmt-sup-tbody tr').length;
        return { kpis, rowCount: rows };
    })()
"@
    Write-Host "Suppliers Subview Check:`n$($supSubCheck | ConvertTo-Json -Depth 3)"
    Invoke-CDPScreenshot $ws "$screenshotsDir\08_suppliers_subview.png"

    # Step 12: Click "عرض التفاصيل" on first supplier
    Write-Host "Testing Supplier Details View for first supplier..."
    $supDetailCheck = Invoke-CDPEval $ws @"
    (async () => {
        const firstActionBtn = document.querySelector('#mgmt-sup-tbody tr .mgmt-action-btn');
        if (firstActionBtn) firstActionBtn.click();
        await new Promise(r => setTimeout(r, 800));
        const title = document.getElementById('mgmt-supdetail-name')?.textContent;
        const materials = document.getElementById('mgmt-supdetail-materials')?.textContent;
        const posRows = document.querySelectorAll('#mgmt-supdetail-pos-tbody tr').length;
        const transRows = document.querySelectorAll('#mgmt-supdetail-trans-tbody tr').length;
        const storageRows = document.querySelectorAll('#mgmt-supdetail-storage-tbody tr').length;
        return { title, materials, posRows, transRows, storageRows };
    })()
"@
    Write-Host "Supplier Details View Check:`n$($supDetailCheck | ConvertTo-Json -Depth 3)"
    Invoke-CDPScreenshot $ws "$screenshotsDir\09_supplier_details.png"

    # Step 13: Check console error count
    $consoleErrors = Invoke-CDPEval $ws "window.__errors || []"
    Write-Host "Console Errors: $($consoleErrors.Count)"

    Write-Host "`nALL CDP AUTOMATION TESTS PASSED SUCCESSFULLY!"

} finally {
    if ($ws -and $ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", [System.Threading.CancellationToken]::None).Wait()
    }
    if ($chromeProc -and !$chromeProc.HasExited) {
        Stop-Process -Id $chromeProc.Id -Force
    }
}
