$ErrorActionPreference = 'Stop'
$baseUrl = "http://localhost:5124"
$results = @()

function Record-Test($name, $passed, $details) {
    $statusStr = if ($passed) { "PASS" } else { "FAIL" }
    $script:results += [PSCustomObject]@{
        Test = $name
        Status = $statusStr
        Details = $details
    }
    $color = if ($passed) { "Green" } else { "Red" }
    Write-Host "[$statusStr] $name - $details" -ForegroundColor $color
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "STARTING FINANCE REDESIGN VERIFICATION SUITE" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Login as Finance Director / CEO (kareem_ceo)
$loginCeoPayload = @{
    identifier = "kareem_ceo"
    password = "Password123!"
} | ConvertTo-Json

try {
    $ceoLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginCeoPayload -ContentType "application/json"
    Record-Test "Auth - Login as Finance Director (CEO)" ($ceoLogin.success -eq $true) "Token: $($ceoLogin.data.token.Substring(0, 15))..."
    $ceoToken = $ceoLogin.data.token
    $ceoHeaders = @{ "Authorization" = "Bearer $ceoToken" }
} catch {
    Record-Test "Auth - Login as Finance Director (CEO)" $false $_.Exception.Message
}

# 2. Login as Non-Finance / Restricted User (storage@facestone.com)
try {
    $nonFinLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body (@{ identifier = "storage@facestone.com"; password = "Storage123!" } | ConvertTo-Json) -ContentType "application/json"
    $nonFinToken = $nonFinLogin.data.token
    $nonFinHeaders = @{ "Authorization" = "Bearer $nonFinToken" }
    Record-Test "Auth - Login as Storage User" ($nonFinLogin.success -eq $true) "User: $($nonFinLogin.data.user.username)"
} catch {
    Record-Test "Auth - Login as Storage User" $false $_.Exception.Message
}

# 3. Test Direct HTML Routes (SPA Entry Points)
$directRoutes = @(
    "/finance",
    "/finance/history",
    "/finance/suppliers",
    "/finance/purchase-orders",
    "/finance/performance",
    "/finance/bank",
    "/finance/expenses",
    "/finance/employees"
)
foreach ($r in $directRoutes) {
    try {
        $htmlRes = Invoke-WebRequest -Uri "$baseUrl$r" -Headers @{ "Accept" = "text/html" } -Method Get
        $isOk = ($htmlRes.StatusCode -eq 200 -and $htmlRes.Content.Contains("finance-hub-cards-view"))
        Record-Test "SPA HTML Direct Route: $r" $isOk "Status: $($htmlRes.StatusCode)"
    } catch {
        Record-Test "SPA HTML Direct Route: $r" $false $_.Exception.Message
    }
}

# 4. CARD 1: HISTORY (GET /api/finance/history)
try {
    $histRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/history?page=1&pageSize=10" -Headers $ceoHeaders -Method Get
    $hasItems = $histRes.success -eq $true -and $histRes.data.items -ne $null
    Record-Test "Card 1: GET /api/finance/history" $hasItems "Total Records: $($histRes.data.totalRecords), Returned: $($histRes.data.items.Count)"
} catch {
    Record-Test "Card 1: GET /api/finance/history" $false $_.Exception.Message
}

# 5. CARD 2: SUPPLIERS (GET /api/finance/suppliers & /api/finance/suppliers/{id})
try {
    $supRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/suppliers" -Headers $ceoHeaders -Method Get
    $hasSuppliers = $supRes.success -eq $true -and $supRes.data.Count -gt 0
    $firstSupplierName = if ($hasSuppliers) { $supRes.data[0].supplierName } else { "" }
    Record-Test "Card 2: GET /api/finance/suppliers" $hasSuppliers "Suppliers Count: $($supRes.data.Count), First: $firstSupplierName"

    if ($hasSuppliers -and $firstSupplierName) {
        $encodedName = [System.Uri]::EscapeDataString($firstSupplierName)
        $supDetailRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/suppliers/$encodedName" -Headers $ceoHeaders -Method Get
        $detailOk = $supDetailRes.success -eq $true -and $supDetailRes.data.supplierName -eq $firstSupplierName
        Record-Test "Card 2: GET /api/finance/suppliers/{id}" $detailOk "Supplier: $($supDetailRes.data.supplierName), POs: $($supDetailRes.data.purchaseOrders.Count), Deliveries: $($supDetailRes.data.recentDeliveries.Count)"
    }
} catch {
    Record-Test "Card 2: Suppliers Endpoints" $false $_.Exception.Message
}

# 6. CARD 3: PURCHASE ORDERS (GET /api/finance/purchase-orders & /{poNumber})
try {
    $posRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/purchase-orders" -Headers $ceoHeaders -Method Get
    $hasPos = $posRes.success -eq $true -and $posRes.data.Count -gt 0
    $firstPoNumber = if ($hasPos) { $posRes.data[0].poNumber } else { "" }
    Record-Test "Card 3: GET /api/finance/purchase-orders" $hasPos "Total POs: $($posRes.data.Count), First: $firstPoNumber"

    if ($hasPos -and $firstPoNumber) {
        $poDetailRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/purchase-orders/$firstPoNumber" -Headers $ceoHeaders -Method Get
        $poDetailOk = $poDetailRes.success -eq $true -and $poDetailRes.data.poNumber -eq $firstPoNumber
        Record-Test "Card 3: GET /api/finance/purchase-orders/{poNumber}" $poDetailOk "PO: $($poDetailRes.data.poNumber), Expected: $($poDetailRes.data.originalQuantity)t, Delivered: $($poDetailRes.data.deliveredQuantity)t"
    }
} catch {
    Record-Test "Card 3: Purchase Orders Endpoints" $false $_.Exception.Message
}

# 7. CARD 4: PERFORMANCE & FORECAST (GET /api/finance/performance)
try {
    $todayStr = (Get-Date).ToString("yyyy-MM-dd")
    $perfRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/performance?fromDate=$todayStr&toDate=$todayStr" -Headers $ceoHeaders -Method Get
    $hasPerf = $perfRes.success -eq $true -and $perfRes.data -ne $null
    $hasForecast = $hasPerf -and $perfRes.data.forecasting -ne $null
    Record-Test "Card 4: GET /api/finance/performance (Calculations)" $hasPerf "Expected Qty: $($perfRes.data.expectedQuantity)t, Delivered: $($perfRes.data.deliveredQuantity)t, Waste: $($perfRes.data.totalWasteQuantity)t"
    Record-Test "Card 4: Forecasting Data Structure" $hasForecast "Forecast Rem Qty: $($perfRes.data.forecasting.expectedRemainingQuantity)t, Rem Value: $($perfRes.data.forecasting.expectedRemainingValue) EGP, Est Days: $($perfRes.data.forecasting.estimatedDaysToCompletion)"
} catch {
    Record-Test "Card 4: Performance & Forecast" $false $_.Exception.Message
}

# 8. CARD 5: BANK (GET /api/finance/bank & POST /api/finance/bank)
try {
    $bankSummaryBefore = Invoke-RestMethod -Uri "$baseUrl/api/finance/bank" -Headers $ceoHeaders -Method Get
    $bankOk = $bankSummaryBefore.success -eq $true -and $bankSummaryBefore.data -ne $null
    Record-Test "Card 5: GET /api/finance/bank" $bankOk "Current Balance: $($bankSummaryBefore.data.currentBalance) EGP, Trans Count: $($bankSummaryBefore.data.totalTransactionsCount)"

    $initialBalance = [decimal]$bankSummaryBefore.data.currentBalance

    # Add a test deposit transaction
    $testBankPayload = @{
        transactionDate = (Get-Date).ToString("yyyy-MM-dd")
        bankName = "National Bank of Egypt"
        transactionType = "Wire Transfer"
        direction = "In"
        amount = 150000.00
        referenceNumber = "REF-BNK-TEST-" + (Get-Random -Minimum 1000 -Maximum 9999)
        description = "Contractual installment deposit"
        notes = "Automated test verification transaction"
    } | ConvertTo-Json

    $addBankRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/bank" -Headers $ceoHeaders -Method Post -Body $testBankPayload -ContentType "application/json"
    $bankAddedOk = $addBankRes.success -eq $true
    Record-Test "Card 5: POST /api/finance/bank (Add Transaction)" $bankAddedOk "Message: $($addBankRes.message), Trans Id: $($addBankRes.data.id)"

    # Verify balance updated
    $bankSummaryAfter = Invoke-RestMethod -Uri "$baseUrl/api/finance/bank" -Headers $ceoHeaders -Method Get
    $expectedBal = $initialBalance + 150000.00
    $balanceCorrect = ([decimal]$bankSummaryAfter.data.currentBalance -eq $expectedBal)
    Record-Test "Card 5: Bank Balance Calculation (Received - Sent)" $balanceCorrect "New Balance: $($bankSummaryAfter.data.currentBalance) EGP (Expected: $expectedBal)"
} catch {
    Record-Test "Card 5: Bank Operations" $false $_.Exception.Message
}

# 9. CARD 6: EXPENSES (GET /api/finance/expenses & POST /api/finance/expenses)
try {
    $expSummaryBefore = Invoke-RestMethod -Uri "$baseUrl/api/finance/expenses" -Headers $ceoHeaders -Method Get
    $expOk = $expSummaryBefore.success -eq $true -and $expSummaryBefore.data -ne $null
    Record-Test "Card 6: GET /api/finance/expenses" $expOk "Total Expenses: $($expSummaryBefore.data.totalExpenses) EGP, Count: $($expSummaryBefore.data.expenses.Count)"

    # Add a test expense
    $testExpPayload = @{
        expenseDate = (Get-Date).ToString("yyyy-MM-dd")
        category = "Electricity"
        amount = 25000.00
        title = "Main factory electricity bill"
        beneficiary = "North Cairo Electricity Distribution"
        paymentMethod = "Bank Transfer"
        referenceNumber = "ELEC-" + (Get-Random -Minimum 1000 -Maximum 9999)
        notes = "Real automated verification expense"
    } | ConvertTo-Json

    $addExpRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/expenses" -Headers $ceoHeaders -Method Post -Body $testExpPayload -ContentType "application/json"
    $expAddedOk = $addExpRes.success -eq $true
    Record-Test "Card 6: POST /api/finance/expenses (Add Expense)" $expAddedOk "Message: $($addExpRes.message), Expense Id: $($addExpRes.data.id)"

    # Verify expense statistics
    $expSummaryAfter = Invoke-RestMethod -Uri "$baseUrl/api/finance/expenses" -Headers $ceoHeaders -Method Get
    $catBreakdown = $expSummaryAfter.data.categoryBreakdown | Where-Object { $_.category -eq "Electricity" }
    $catFound = ($catBreakdown -ne $null -and $catBreakdown.totalAmount -ge 25000.00)
    Record-Test "Card 6: Expense Statistics by Category" $catFound "Category 'Electricity' Total: $($catBreakdown.totalAmount) EGP"
} catch {
    Record-Test "Card 6: Expenses Operations" $false $_.Exception.Message
}

# 10. CARD 7: EMPLOYEES (GET /api/finance/employees & /{id})
try {
    $empRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/employees" -Headers $ceoHeaders -Method Get
    $hasEmps = $empRes.success -eq $true -and $empRes.data.Count -gt 0
    $firstEmpId = if ($hasEmps) { $empRes.data[0].id } else { "" }
    Record-Test "Card 7: GET /api/finance/employees" $hasEmps "Employees Count: $($empRes.data.Count), First: $($empRes.data[0].fullName)"

    if ($hasEmps -and $firstEmpId) {
        $empDetailRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/employees/$firstEmpId" -Headers $ceoHeaders -Method Get
        $empDetailOk = $empDetailRes.success -eq $true -and $empDetailRes.data.id -eq $firstEmpId
        Record-Test "Card 7: GET /api/finance/employees/{id}" $empDetailOk "Emp: $($empDetailRes.data.fullName), Dept: $($empDetailRes.data.department), Basic Salary: $($empDetailRes.data.basicSalary)"
    }
} catch {
    Record-Test "Card 7: Employees Endpoints" $false $_.Exception.Message
}

# 11. PAYMENT STATUS: Update by Finance Director & Verification of Persistence
try {
    $testPoNum = "PO-100"
    $newStatus = "Paid in Full"
    $updatePayload = @{
        status = $newStatus
        newStatus = $newStatus
        notes = "Updated payment status via Finance verification test"
    } | ConvertTo-Json

    $updateRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/purchase-orders/$testPoNum/payment-status" -Headers $ceoHeaders -Method Put -Body $updatePayload -ContentType "application/json"
    $updateOk = ($updateRes.success -eq $true -and $updateRes.data.paymentStatus -eq $newStatus)
    Record-Test "Requirement 9: Finance Director Updates Payment Status" $updateOk "Status updated to: $($updateRes.data.paymentStatus)"

    # Verify persistence
    $recheck = Invoke-RestMethod -Uri "$baseUrl/api/finance/purchase-orders/$testPoNum" -Headers $ceoHeaders -Method Get
    $persisted = ($recheck.data.paymentStatus -eq $newStatus)
    Record-Test "Requirement 9: Payment Status Persists in Database" $persisted "Persisted Payment Status: $($recheck.data.paymentStatus)"
} catch {
    Record-Test "Requirement 9: Payment Status Management" $false $_.Exception.Message
}

# 12. PERMISSIONS: Verify Unauthorized User Forbidden from Mutation Endpoints
try {
    $forbiddenExpPayload = @{
        expenseDate = (Get-Date).ToString("yyyy-MM-dd")
        category = "Office"
        amount = 500.00
        title = "Unauthorized expense attempt"
    } | ConvertTo-Json

    $blocked = $false
    try {
        $badRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/expenses" -Headers $nonFinHeaders -Method Post -Body $forbiddenExpPayload -ContentType "application/json"
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 403) { $blocked = $true }
    }
    Record-Test "Requirement 8: Unauthorized User Blocked from Adding Expenses (403)" $blocked "Status: 403 Forbidden"
} catch {
    Record-Test "Requirement 8: Permission Enforcement" $false $_.Exception.Message
}

try {
    $blockedPo = $false
    try {
        $badPoRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/purchase-orders/PO-100/payment-status" -Headers $nonFinHeaders -Method Put -Body (@{ status = "Unpaid"; newStatus = "Unpaid" } | ConvertTo-Json) -ContentType "application/json"
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 403) { $blockedPo = $true }
    }
    Record-Test "Requirement 8: Unauthorized User Blocked from Payment Status (403)" $blockedPo "Status: 403 Forbidden"
} catch {
    Record-Test "Requirement 8: Permission Enforcement for PO" $false $_.Exception.Message
}

# 13. INTEGRITY CHECKS: Verify Non-Finance Modules Intact
try {
    # Transportation
    $transCheck = Invoke-RestMethod -Uri "$baseUrl/api/transportation/recent" -Headers $ceoHeaders -Method Get
    Record-Test "Integrity: Transportation Module Intact" ($transCheck.success -eq $true) "Returned Transactions: $($transCheck.data.Count)"
} catch {
    Record-Test "Integrity: Transportation Module Intact" $false $_.Exception.Message
}

try {
    # Storage
    $storageCheck = Invoke-RestMethod -Uri "$baseUrl/api/department/storage" -Headers $ceoHeaders -Method Get
    Record-Test "Integrity: Storage Module Intact" ($storageCheck.success -eq $true) "Status: $($storageCheck.message)"
} catch {
    Record-Test "Integrity: Storage Module Intact" $false $_.Exception.Message
}

try {
    # Management
    $mgmtCheck = Invoke-RestMethod -Uri "$baseUrl/api/department/management" -Headers $ceoHeaders -Method Get
    Record-Test "Integrity: Management Module Intact" ($mgmtCheck.success -eq $true) "Status: $($mgmtCheck.message)"
} catch {
    Record-Test "Integrity: Management Module Intact" $false $_.Exception.Message
}

try {
    # Global Chat Conversations
    $chatCheck = Invoke-RestMethod -Uri "$baseUrl/api/chat/conversations" -Headers $ceoHeaders -Method Get
    Record-Test "Integrity: Global Chat Module Intact" ($chatCheck.success -eq $true) "Conversations: $($chatCheck.data.Count)"
} catch {
    Record-Test "Integrity: Global Chat Module Intact" $false $_.Exception.Message
}

$passCount = ($results | Where-Object { $_.Status -eq "PASS" }).Count
$failCount = ($results | Where-Object { $_.Status -eq "FAIL" }).Count
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "SUMMARY: $passCount PASSED / $failCount FAILED" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
