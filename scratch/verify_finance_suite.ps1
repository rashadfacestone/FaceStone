# Verification test suite for FaceStone Finance Department Redirection & Management
$baseUrl = "http://localhost:5124"

# 1. Start application process if not running
$tcpConn = Get-NetTCPConnection -LocalPort 5124 -ErrorAction SilentlyContinue
$startedProcess = $null

if (-not $tcpConn) {
    Write-Host "[TEST] Starting FaceStone.Web on port 5124..."
    $startedProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "FaceStone.Web.csproj", "--no-build" -PassThru -WindowStyle Hidden
    
    # Wait for server to become responsive
    $healthy = $false
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Seconds 1
        try {
            $h = Invoke-RestMethod -Uri "$baseUrl/health" -TimeoutSec 2 -ErrorAction Stop
            if ($h.status -eq "Healthy") {
                $healthy = $true
                Write-Host "[TEST] Server is healthy and responsive."
                break
            }
        } catch {}
    }
    if (-not $healthy) {
        Write-Error "Server failed to start within timeout."
        if ($startedProcess) { Stop-Process -Id $startedProcess.Id -Force }
        exit 1
    }
} else {
    Write-Host "[TEST] Server is already running on port 5124."
}

try {
    # Helper to authenticate
    function Get-Token($identifier, $password) {
        $body = @{ identifier = $identifier; password = $password } | ConvertTo-Json
        $res = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $body -ContentType "application/json"
        return $res.data.token
    }

    Write-Host "`n=== TEST 1: AUTHORIZATION CHECKS ==="
    
    # 1.1 Accountant
    Write-Host "[TEST 1.1] Testing Accountant (sarah@facestone.com)..."
    $accToken = Get-Token "sarah@facestone.com" "SecretPass123!"
    $accHeaders = @{ Authorization = "Bearer $accToken" }
    
    $accVerify = Invoke-RestMethod -Uri "$baseUrl/api/department/finance" -Method Get -Headers $accHeaders
    Write-Host "  Accountant /api/department/finance: success=$($accVerify.success)"
    if ($accVerify.success -ne $true) { throw "Accountant should be authorized for Finance Department" }

    # 1.2 CEO
    Write-Host "[TEST 1.2] Testing CEO (kareem.ceo@facestone.com)..."
    $ceoToken = Get-Token "kareem.ceo@facestone.com" "Password123!"
    $ceoHeaders = @{ Authorization = "Bearer $ceoToken" }

    $ceoVerify = Invoke-RestMethod -Uri "$baseUrl/api/department/finance" -Method Get -Headers $ceoHeaders
    Write-Host "  CEO /api/department/finance: success=$($ceoVerify.success)"
    if ($ceoVerify.success -ne $true) { throw "CEO should be authorized for Finance Department" }

    # 1.3 IT
    Write-Host "[TEST 1.3] Testing IT (it@facestone.com)..."
    $itToken = Get-Token "it@facestone.com" "Password123!"
    $itHeaders = @{ Authorization = "Bearer $itToken" }

    $itVerify = Invoke-RestMethod -Uri "$baseUrl/api/department/finance" -Method Get -Headers $itHeaders
    Write-Host "  IT /api/department/finance: success=$($itVerify.success)"
    if ($itVerify.success -ne $true) { throw "IT should be authorized for Finance Department" }

    # 1.4 Unauthorized role: Senior Structural Engineer / Employee (alex@facestone.com)
    Write-Host "[TEST 1.4] Testing Employee / Engineer (alex@facestone.com)..."
    $empToken = Get-Token "alex@facestone.com" "Password123!"
    $empHeaders = @{ Authorization = "Bearer $empToken" }

    $empForbidden = $false
    try {
        Invoke-RestMethod -Uri "$baseUrl/api/department/finance" -Method Get -Headers $empHeaders -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 403) {
            $empForbidden = $true
            Write-Host "  Employee /api/department/finance correctly denied with 403 Forbidden."
        }
    }
    if (-not $empForbidden) { throw "Employee should be forbidden (403) from Finance Department access." }

    $empDataForbidden = $false
    try {
        Invoke-RestMethod -Uri "$baseUrl/api/finance/management-data" -Method Get -Headers $empHeaders -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 403) {
            $empDataForbidden = $true
            Write-Host "  Employee /api/finance/management-data correctly denied with 403 Forbidden."
        }
    }
    if (-not $empDataForbidden) { throw "Employee should be forbidden (403) from Finance Management data API." }

    # 1.5 Anonymous caller
    Write-Host "[TEST 1.5] Testing Anonymous caller..."
    $anonDenied = $false
    try {
        Invoke-RestMethod -Uri "$baseUrl/api/department/finance" -Method Get -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 401) {
            $anonDenied = $true
            Write-Host "  Anonymous caller correctly denied with 401 Unauthorized."
        }
    }
    if (-not $anonDenied) { throw "Anonymous caller should be unauthorized (401)." }

    Write-Host "`n=== TEST 2: FINANCE MANAGEMENT DATA & 17 FIELDS ==="
    $dataRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/management-data" -Method Get -Headers $accHeaders
    Write-Host "  Response Success: $($dataRes.success)"
    Write-Host "  Total PO Count: $($dataRes.data.totalCount)"
    Write-Host "  KPI Total PO Value: $($dataRes.data.kpiSummary.totalPoValue) EGP"
    Write-Host "  KPI Total Supplied Quantity: $($dataRes.data.kpiSummary.totalSuppliedQuantity) Tons"
    Write-Host "  KPI Total Remaining Quantity: $($dataRes.data.kpiSummary.totalRemainingQuantity) Tons"
    Write-Host "  KPI Total Paid Amount: $($dataRes.data.kpiSummary.totalPaidAmount) EGP"
    Write-Host "  KPI Total Outstanding: $($dataRes.data.kpiSummary.totalOutstandingAmount) EGP"
    Write-Host "  KPI Total Movements: $($dataRes.data.kpiSummary.totalMovementsCount)"

    $records = $dataRes.data.records
    if ($records.Count -eq 0) { throw "Finance records should not be empty." }

    Write-Host "`n  Sample Record Check (First PO):"
    $r = $records[0]
    Write-Host "    1. PO: $($r.poNumber)"
    Write-Host "    2. Supplier: $($r.supplier)"
    Write-Host "    3. Order Date: $($r.orderDate)"
    Write-Host "    4. PO Quantity: $($r.poQuantity)"
    Write-Host "    5. PO Value: $($r.poValue)"
    Write-Host "    6. Ton Price: $($r.tonPrice)"
    Write-Host "    7. Supplied Qty: $($r.suppliedQuantity)"
    Write-Host "    8. Supplied Value: $($r.suppliedValue)"
    Write-Host "    9. Remaining Qty: $($r.remainingQuantity)"
    Write-Host "    10. Remaining Value: $($r.remainingValue)"
    Write-Host "    11. Movements Count: $($r.movementsCount)"
    Write-Host "    12. Approved Qty: $($r.approvedQuantity)"
    Write-Host "    13. Unapproved Qty: $($r.unapprovedQuantity)"
    Write-Host "    14. Paid Amount: $($r.paidAmount)"
    Write-Host "    15. Outstanding Amount: $($r.outstandingAmount)"
    Write-Host "    16. Payment Status: $($r.paymentStatus)"
    Write-Host "    17. Invoice Number: $($r.invoiceNumber)"

    # Verify Calculations
    Write-Host "`n=== TEST 3: MATHEMATICAL CALCULATIONS VERIFICATION ==="
    foreach ($rec in $records) {
        $expectedPoVal = [Math]::Round($rec.poQuantity * $rec.tonPrice, 2)
        if ([Math]::Abs($rec.poValue - $expectedPoVal) -gt 0.05) {
            throw "PO Value mismatch for $($rec.poNumber): Actual=$($rec.poValue), Expected=$expectedPoVal"
        }

        $expectedSuppliedVal = [Math]::Round($rec.suppliedQuantity * $rec.tonPrice, 2)
        if ([Math]::Abs($rec.suppliedValue - $expectedSuppliedVal) -gt 0.05) {
            throw "Supplied Value mismatch for $($rec.poNumber): Actual=$($rec.suppliedValue), Expected=$expectedSuppliedVal"
        }

        $expectedRemQty = [Math]::Round([Math]::Max(0.0, $rec.poQuantity - $rec.suppliedQuantity), 3)
        if ([Math]::Abs($rec.remainingQuantity - $expectedRemQty) -gt 0.05) {
            throw "Remaining Qty mismatch for $($rec.poNumber): Actual=$($rec.remainingQuantity), Expected=$expectedRemQty"
        }

        $expectedRemVal = [Math]::Round($rec.remainingQuantity * $rec.tonPrice, 2)
        if ([Math]::Abs($rec.remainingValue - $expectedRemVal) -gt 0.05) {
            throw "Remaining Value mismatch for $($rec.poNumber): Actual=$($rec.remainingValue), Expected=$expectedRemVal"
        }

        $expectedOutstanding = [Math]::Round([Math]::Max(0.0, $rec.suppliedValue - $rec.paidAmount), 2)
        if ([Math]::Abs($rec.outstandingAmount - $expectedOutstanding) -gt 0.05) {
            throw "Outstanding Amount mismatch for $($rec.poNumber): Actual=$($rec.outstandingAmount), Expected=$expectedOutstanding"
        }

        # Payment status verification (using Unicode regex for PowerShell 5.1 compatibility)
        if ($rec.paidAmount -le 0) {
            # "غير مدفوع" contains \u063a (Ghein)
            if ($rec.paymentStatus -notmatch "[\u063a]") { throw "Status should be Unpaid when paid amount is 0. Got: $($rec.paymentStatus)" }
        } elseif ($rec.paidAmount -gt 0 -and $rec.paidAmount -lt $rec.suppliedValue) {
            # "مدفوع جزئياً" contains \u062c (Jeem)
            if ($rec.paymentStatus -notmatch "[\u062c]") { throw "Status should be Partially Paid. Got: $($rec.paymentStatus)" }
        } else {
            # "مدفوع بالكامل" contains \u0643 (Kaaf)
            if ($rec.paymentStatus -notmatch "[\u0643]") { throw "Status should be Fully Paid. Got: $($rec.paymentStatus)" }
        }
    }
    Write-Host "  All financial formulas verified successfully across all $($records.Count) records!"

    Write-Host "`n=== TEST 4: SEARCH & FILTERING ==="
    # Filter by specific PO
    $poSearch = Invoke-RestMethod -Uri "$baseUrl/api/finance/management-data?search=PO-2026-9810" -Method Get -Headers $accHeaders
    Write-Host "  Search PO-2026-9810 found: $($poSearch.data.totalCount)"
    if ($poSearch.data.totalCount -lt 1) { throw "Search should find PO-2026-9810" }

    Write-Host "`n=== TEST 5: CEO PAYMENT ARCHITECTURE ==="
    # CEO records a partial payment of 50,000 EGP for PO-2026-9810
    $paymentObj = @{
        poNumber = "PO-2026-9810"
        amount = 50000.00
        paymentMethod = "Bank Transfer"
        referenceNumber = "CIB-TRF-2026-9901"
        notes = "CEO Payment Test"
    }
    $paymentBody = $paymentObj | ConvertTo-Json

    $payRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/payments" -Method Post -Body $paymentBody -ContentType "application/json" -Headers $ceoHeaders
    Write-Host "  CEO Payment recorded: $($payRes.success) - Amount: $($payRes.data.amount) EGP by $($payRes.data.paidBy)"

    # Verify Finance Management automatically reflects this payment
    $poAfterPay = Invoke-RestMethod -Uri "$baseUrl/api/finance/management-data?search=PO-2026-9810" -Method Get -Headers $accHeaders
    $recAfter = $poAfterPay.data.records[0]
    Write-Host "  PO-2026-9810 Paid Amount after CEO payment: $($recAfter.paidAmount) EGP"
    Write-Host "  PO-2026-9810 Outstanding Amount after CEO payment: $($recAfter.outstandingAmount) EGP"
    Write-Host "  PO-2026-9810 Payment Status after CEO payment: $($recAfter.paymentStatus)"
    if ($recAfter.paidAmount -lt 50000) { throw "Paid amount should be at least 50000 EGP" }
    if ($recAfter.paymentStatus -notmatch "[\u062c]") { throw "Status should be Partially Paid after partial payment. Got: $($recAfter.paymentStatus)" }

    # Query payment history for this PO
    $histRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/payments/PO-2026-9810" -Method Get -Headers $accHeaders
    Write-Host "  Payment history records count for PO-2026-9810: $($histRes.data.Count)"
    if ($histRes.data.Count -lt 1) { throw "Payment history should return at least 1 payment" }

    Write-Host "`n=== TEST 6: EXISTING FINANCE CHAT VERIFICATION ==="
    # Verify Accountant can load conversations
    $convRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/conversations" -Method Get -Headers $accHeaders
    Write-Host "  Chat conversations endpoint accessible: $($convRes.success)"

    # Verify Employee can load desk messages
    $empConvRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/messages" -Method Get -Headers $empHeaders
    Write-Host "  Employee chat endpoint accessible: $($empConvRes.success)"

    Write-Host "`n==============================================="
    Write-Host "ALL 6 VERIFICATION TEST SUITES PASSED SUCCESSFULLY!"
    Write-Host "==============================================="
} catch {
    Write-Error "Test failed: $_"
    exit 1
} finally {
    if ($startedProcess) {
        Write-Host "Stopping test server process..."
        Stop-Process -Id $startedProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
