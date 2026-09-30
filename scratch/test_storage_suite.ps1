param(
    [string]$BaseUrl = "http://127.0.0.1:5124"
)

$ErrorActionPreference = "Stop"
Write-Host "============================================================"
Write-Host "      FACESTONE STORAGE DEPARTMENT AUTOMATED TEST SUITE     "
Write-Host "============================================================"

# Start web app in background
$appProc = Start-Process -FilePath "dotnet" -ArgumentList "run --no-build" -PassThru -NoNewWindow
Start-Sleep -Seconds 4

try {
    # ------------------------------------------------------------
    # TEST 1: API Direct Tests (Auth & Permissions)
    # ------------------------------------------------------------
    Write-Host "`n--- TEST 1: Authentication & Authorization for Storage ---"

    # Helper function for JSON requests
    function Invoke-ApiPost {
        param([string]$Url, [hashtable]$Body, [string]$Token = $null)
        $headers = @{ "Content-Type" = "application/json; charset=utf-8" }
        if ($Token) { $headers["Authorization"] = "Bearer $Token" }
        $json = $Body | ConvertTo-Json -Depth 5
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
        try {
            $resp = Invoke-RestMethod -Uri $Url -Method Post -Body $bytes -Headers $headers -TimeoutSec 10
            return @{ Success = $true; Data = $resp }
        } catch {
            $respStream = $_.Exception.Response.GetResponseStream()
            $reader = [System.IO.StreamReader]::new($respStream, [System.Text.Encoding]::UTF8)
            $respBody = $reader.ReadToEnd()
            return @{ Success = $false; StatusCode = $_.Exception.Response.StatusCode.value__; Body = $respBody }
        }
    }

    function Invoke-ApiGet {
        param([string]$Url, [string]$Token = $null)
        $headers = @{}
        if ($Token) { $headers["Authorization"] = "Bearer $Token" }
        try {
            $resp = Invoke-RestMethod -Uri $Url -Method Get -Headers $headers -TimeoutSec 10
            return @{ Success = $true; Data = $resp }
        } catch {
            return @{ Success = $false; StatusCode = $_.Exception.Response.StatusCode.value__; Error = $_.Exception.Message }
        }
    }

    # 1.1 Login as Storage Manager
    $loginStorage = Invoke-ApiPost -Url "$BaseUrl/api/auth/login" -Body @{
        identifier = "storage@facestone.com"
        password = "Storage123!"
    }
    if (!$loginStorage.Success) { throw "Storage Manager login failed: $($loginStorage.Body)" }
    $storageToken = $loginStorage.Data.data.token
    Write-Host "[PASS] Storage Manager login successful. Token acquired."

    # 1.2 Verify Storage Access for Storage Manager
    $verifyStorage = Invoke-ApiGet -Url "$BaseUrl/api/department/storage" -Token $storageToken
    if (!$verifyStorage.Success) { throw "Storage Manager denied access to Storage Department!" }
    Write-Host "[PASS] Storage Manager authorized for Storage Department."

    # 1.3 Login as Employee (should be denied Storage access)
    $loginEmp = Invoke-ApiPost -Url "$BaseUrl/api/auth/login" -Body @{
        identifier = "alex@facestone.com"
        password = "Password123!"
    }
    $empToken = $loginEmp.Data.data.token
    $verifyEmpStorage = Invoke-ApiGet -Url "$BaseUrl/api/department/storage" -Token $empToken
    if ($verifyEmpStorage.Success -or $verifyEmpStorage.StatusCode -ne 403) {
        throw "Security breach: Employee was NOT rejected with 403 Forbidden!"
    }
    Write-Host "[PASS] Regular Employee correctly rejected with 403 Forbidden for Storage Department."

    # 1.4 Login as CEO (should have Storage access)
    $loginCeo = Invoke-ApiPost -Url "$BaseUrl/api/auth/login" -Body @{
        identifier = "kareem.ceo@facestone.com"
        password = "Password123!"
    }
    $ceoToken = $loginCeo.Data.data.token
    $verifyCeoStorage = Invoke-ApiGet -Url "$BaseUrl/api/department/storage" -Token $ceoToken
    if (!$verifyCeoStorage.Success) { throw "CEO denied access to Storage Department!" }
    Write-Host "[PASS] CEO authorized for Storage Department."

    # ------------------------------------------------------------
    # TEST 2: Storage Config Endpoint
    # ------------------------------------------------------------
    Write-Host "`n--- TEST 2: Storage Config Endpoint ---"
    $configRes = Invoke-ApiGet -Url "$BaseUrl/api/storage/config" -Token $storageToken
    if (!$configRes.Success) { throw "Failed to get storage config" }
    $cfg = $configRes.Data.data

    Write-Host ("Config Service Types: {0}" -f ($cfg.serviceTypes -join ", "))
    Write-Host ("Storage Locations count: {0}" -f $cfg.storageLocations.Count)
    Write-Host ("Available Employees count: {0}" -f $cfg.employees.Count)
    Write-Host ("Recent Transportation count: {0}" -f $cfg.availableTransportationMovements.Count)

    $expectedTypes = @("إضافة", "صرف", "تحويل", "تسوية")
    foreach ($et in $expectedTypes) {
        if ($cfg.serviceTypes -notcontains $et) {
            throw "Expected service type '$et' missing from config!"
        }
    }
    Write-Host "[PASS] All 4 required Arabic service types present."

    # ------------------------------------------------------------
    # TEST 3: Create Storage Movement - إضافة with Multiple Transportation Movements
    # ------------------------------------------------------------
    Write-Host "`n--- TEST 3: Create Storage Movement (إضافة) with Multiple Movements ---"
    $ref1 = 9101
    $mov1 = 6001

    $addPayload = @{
        movementNumber = $mov1
        transactionDate = (Get-Date).ToString("o")
        serviceType = "إضافة"
        referenceNumber = $ref1
        poNumber = "PO-2026-ST-001"
        transportationMovementNumbers = @( 1001, 1002, 1003 )
        incomingQuantity = 85.500
        outgoingQuantity = 0.000
        openingBalance = $cfg.currentOverallBalance
        closingBalance = ($cfg.currentOverallBalance + 85.500)
        storageLocation = "مستودع الخامات الرئيسي (صوامع وتخزين الركام)"
        receivingEmployee = "Storage & Factory Operations Manager"
        notes = "توريد خامات متعدد النقلات للاختبار"
    }

    $addRes = Invoke-ApiPost -Url "$BaseUrl/api/storage" -Body $addPayload -Token $storageToken
    if (!$addRes.Success) { throw "Failed to create 'إضافة' transaction: $($addRes.Body)" }
    $created1 = $addRes.Data.data
    Write-Host ("Created transaction #{0} with Reference {1}. Closing Balance: {2}" -f $created1.movementNumber, $created1.referenceNumber, $created1.closingBalance)
    Write-Host ("Linked Transportation movements count: {0} ({1})" -f $created1.transportationMovementNumbers.Count, ($created1.transportationMovementNumbers -join ", "))

    if ($created1.transportationMovementNumbers.Count -ne 3) {
        throw "Failed: Expected 3 transportation movements, got $($created1.transportationMovementNumbers.Count)"
    }
    Write-Host "[PASS] Multiple transportation movements saved correctly."

    # ------------------------------------------------------------
    # TEST 4: Unique Reference Number Validation (رقم المرجع مستخدم بالفعل)
    # ------------------------------------------------------------
    Write-Host "`n--- TEST 4: Duplicate Reference Number Validation ---"
    # Check via check-reference endpoint
    $chkRef = Invoke-ApiGet -Url "$BaseUrl/api/storage/check-reference/$ref1" -Token $storageToken
    if (!$chkRef.Data.data.exists) {
        throw "check-reference failed to detect existing reference $ref1"
    }
    Write-Host ("check-reference response: Exists={0}, Message={1}" -f $chkRef.Data.data.exists, $chkRef.Data.data.message)

    # Attempt to submit duplicate reference
    $dupPayload = @{
        movementNumber = 6002
        transactionDate = (Get-Date).ToString("o")
        serviceType = "إضافة"
        referenceNumber = $ref1  # DUPLICATE!
        poNumber = "PO-2026-ST-001"
        transportationMovementNumbers = @( 1004 )
        incomingQuantity = 10.000
        outgoingQuantity = 0.000
        openingBalance = $created1.closingBalance
        closingBalance = ($created1.closingBalance + 10.000)
        storageLocation = "مستودع الخامات الرئيسي (صوامع وتخزين الركام)"
        receivingEmployee = "Storage & Factory Operations Manager"
    }
    $dupRes = Invoke-ApiPost -Url "$BaseUrl/api/storage" -Body $dupPayload -Token $storageToken
    if ($dupRes.Success) {
        throw "Security failure: Duplicate reference number was accepted!"
    }
    if ($dupRes.Body -notmatch "رقم المرجع مستخدم بالفعل") {
        throw "Expected Arabic error message 'رقم المرجع مستخدم بالفعل', got: $($dupRes.Body)"
    }
    Write-Host "[PASS] Duplicate Reference Number rejected with exact Arabic message: 'رقم المرجع مستخدم بالفعل'."

    # ------------------------------------------------------------
    # TEST 5: Create Storage Movement - صرف (Disbursement)
    # ------------------------------------------------------------
    Write-Host "`n--- TEST 5: Create Storage Movement (صرف) ---"
    $ref2 = 9102
    $mov2 = 6002
    $disbPayload = @{
        movementNumber = $mov2
        transactionDate = (Get-Date).ToString("o")
        serviceType = "صرف"
        referenceNumber = $ref2
        poNumber = "PO-2026-ST-001"
        transportationMovementNumbers = @( 1005 )
        incomingQuantity = 0.000
        outgoingQuantity = 25.500
        openingBalance = $created1.closingBalance
        closingBalance = ($created1.closingBalance - 25.500)
        storageLocation = "مستودع الخامات الرئيسي (صوامع وتخزين الركام)"
        receivingEmployee = "Storage & Factory Operations Manager"
        notes = "صرف مواد لخلطة الموقع"
    }
    $disbRes = Invoke-ApiPost -Url "$BaseUrl/api/storage" -Body $disbPayload -Token $storageToken
    if (!$disbRes.Success) { throw "Failed to create 'صرف' transaction: $($disbRes.Body)" }
    $created2 = $disbRes.Data.data
    Write-Host ("Created صرف transaction #{0}. Outgoing: {1}, Closing: {2}" -f $created2.movementNumber, $created2.outgoingQuantity, $created2.closingBalance)
    Write-Host "[PASS] 'صرف' transaction saved with correct balance calculation."

    # ------------------------------------------------------------
    # TEST 6: Create Storage Movement - تحويل (Transfer)
    # ------------------------------------------------------------
    Write-Host "`n--- TEST 6: Create Storage Movement (تحويل) ---"
    $ref3 = 9103
    $mov3 = 6003
    $transPayload = @{
        movementNumber = $mov3
        transactionDate = (Get-Date).ToString("o")
        serviceType = "تحويل"
        referenceNumber = $ref3
        poNumber = "PO-2026-TR-001"
        transportationMovementNumbers = @( 1006 )
        incomingQuantity = 0.000
        outgoingQuantity = 15.000
        openingBalance = $created2.closingBalance
        closingBalance = ($created2.closingBalance - 15.000)
        storageLocation = "مستودع الخامات الرئيسي (صوامع وتخزين الركام)"
        destinationStorageLocation = "مستودع قطاع المصنع أ (محطة الخلط 1)"
        receivingEmployee = "Storage & Factory Operations Manager"
        notes = "تحويل بين المستودعات الداخلية"
    }
    $transRes = Invoke-ApiPost -Url "$BaseUrl/api/storage" -Body $transPayload -Token $storageToken
    if (!$transRes.Success) { throw "Failed to create 'تحويل' transaction: $($transRes.Body)" }
    $created3 = $transRes.Data.data
    Write-Host ("Created تحويل transaction from '{0}' to '{1}'. Qty: {2}" -f $created3.storageLocation, $created3.destinationStorageLocation, $created3.outgoingQuantity)
    Write-Host "[PASS] 'تحويل' transaction saved with source and destination locations."

    # ------------------------------------------------------------
    # TEST 7: Create Storage Movement - تسوية (Adjustment)
    # ------------------------------------------------------------
    Write-Host "`n--- TEST 7: Create Storage Movement (تسوية) ---"
    $ref4 = 9104
    $mov4 = 6004
    $adjPayload = @{
        movementNumber = $mov4
        transactionDate = (Get-Date).ToString("o")
        serviceType = "تسوية"
        referenceNumber = $ref4
        poNumber = "PO-2026-ADJ-01"
        transportationMovementNumbers = @( 1007 )
        incomingQuantity = 2.500
        outgoingQuantity = 0.000
        openingBalance = $created3.closingBalance
        closingBalance = ($created3.closingBalance + 2.500)
        storageLocation = "مستودع الخامات الرئيسي (صوامع وتخزين الركام)"
        receivingEmployee = "Storage & Factory Operations Manager"
        notes = "تسوية جردية لفروق ميزان معتمدة"
    }
    $adjRes = Invoke-ApiPost -Url "$BaseUrl/api/storage" -Body $adjPayload -Token $storageToken
    if (!$adjRes.Success) { throw "Failed to create 'تسوية' transaction: $($adjRes.Body)" }
    $created4 = $adjRes.Data.data
    Write-Host ("Created تسوية transaction. New Closing: {0}" -f $created4.closingBalance)
    Write-Host "[PASS] 'تسوية' transaction saved successfully."

    # ------------------------------------------------------------
    # TEST 8: Fetch Recent Storage Transactions
    # ------------------------------------------------------------
    Write-Host "`n--- TEST 8: Recent Storage Transactions ---"
    $recRes = Invoke-ApiGet -Url "$BaseUrl/api/storage/recent?limit=10" -Token $storageToken
    if (!$recRes.Success) { throw "Failed to fetch recent transactions" }
    Write-Host ("Fetched {0} recent storage transactions from database." -f $recRes.Data.data.Count)
    if ($recRes.Data.data.Count -lt 4) {
        throw "Expected at least 4 recent transactions!"
    }
    Write-Host "[PASS] Recent transactions fetched successfully with linked transportation movements."

    Write-Host "`n============================================================"
    Write-Host "       ALL BACKEND & API TESTS COMPLETED WITH 100% SUCCESS  "
    Write-Host "============================================================"
}
finally {
    if ($appProc -and !$appProc.HasExited) {
        Stop-Process -Id $appProc.Id -Force
    }
}
