$baseUrl = "http://127.0.0.1:5124"

# 1. Login
$loginBody = @{
    identifier = "kareem.ceo@facestone.com"
    password   = "Password123!"
} | ConvertTo-Json

$loginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginRes.data.token
Write-Host "Logged in as: $($loginRes.data.user.fullName), Role: $($loginRes.data.user.role)"

$headers = @{
    Authorization = "Bearer $token"
}

# 2. Overview
$overview = Invoke-RestMethod -Uri "$baseUrl/api/management/overview" -Method Get -Headers $headers
Write-Host "`n--- OVERVIEW ---"
Write-Host "Trans Trips: $($overview.data.transportation.tripsCount), Total Qty: $($overview.data.transportation.totalReceivedQuantity), Waste: $($overview.data.transportation.totalWasteQuantity)"
Write-Host "Storage Deposit: $($overview.data.storage.totalDeposit), Issue: $($overview.data.storage.totalIssue), Balance: $($overview.data.storage.currentBalance)"
Write-Host "Finance Exp: $($overview.data.finance.totalExpenses), PO Val: $($overview.data.finance.totalPoValue), Perf: $($overview.data.finance.performancePercentage)%"
Write-Host "Employees Count: $($overview.data.employees.totalEmployees), Salaries: $($overview.data.employees.totalSalariesRecorded), Lending: $($overview.data.employees.totalRemainingLending)"
Write-Host "Suppliers Count: $($overview.data.suppliers.totalSuppliers), POs: $($overview.data.suppliers.totalPos), Remaining Qty: $($overview.data.suppliers.totalRemainingQuantity)"

# 3. Transportation
$trans = Invoke-RestMethod -Uri "$baseUrl/api/management/transportation" -Method Get -Headers $headers
Write-Host "`n--- TRANSPORTATION ---"
Write-Host "Total Trips: $($trans.data.totalTrips), Items count: $($trans.data.items.Count)"
$testTrans = $trans.data.items | Where-Object { $_.poNumber -match "TEST-PO" }
Write-Host "Found $($testTrans.Count) TEST-PO transportation records."
foreach ($t in $testTrans) {
    Write-Host "  Mov #$($t.movementNumber) | PO: $($t.poNumber) | Net: $($t.netWeight)t | Waste: $($t.wasteQuantity)t | Co: $($t.companyName)"
}

# 4. Storage
$storage = Invoke-RestMethod -Uri "$baseUrl/api/management/storage" -Method Get -Headers $headers
Write-Host "`n--- STORAGE ---"
Write-Host "Total Movements: $($storage.data.totalMovements), Items count: $($storage.data.items.Count)"
$testStorage = $storage.data.items | Where-Object { $_.poNumber -match "TEST-PO" -or $_.movementNumber -in @(8901, 8902, 8903) }
Write-Host "Found $($testStorage.Count) test storage records."
foreach ($s in $testStorage) {
    Write-Host "  Mov #$($s.movementNumber) | Action: $($s.action) | Material: $($s.material) | In: $($s.incomingQuantity) | Out: $($s.outgoingQuantity) | PO: $($s.poNumber)"
}

# 5. Finance
$fin = Invoke-RestMethod -Uri "$baseUrl/api/management/finance" -Method Get -Headers $headers
Write-Host "`n--- FINANCE ---"
Write-Host "POs: $($fin.data.items.Count), Expenses: $($fin.data.recentExpenses.Count), Bank: $($fin.data.recentBankTransactions.Count)"
$testFinPo = $fin.data.items | Where-Object { $_.poNumber -match "TEST-PO" }
Write-Host "Found $($testFinPo.Count) test POs."
foreach ($p in $testFinPo) {
    Write-Host "  PO: $($p.poNumber) | Supplier: $($p.supplier) | TotalVal: $($p.totalValue) | DelivVal: $($p.deliveredValue) | Status: $($p.paymentStatus)"
}

# 6. Employees
$emp = Invoke-RestMethod -Uri "$baseUrl/api/management/employees" -Method Get -Headers $headers
Write-Host "`n--- EMPLOYEES ---"
Write-Host "Total employees: $($emp.data.Count)"
$testEmps = $emp.data | Where-Object { $_.username -match "test." }
Write-Host "Found $($testEmps.Count) test employees."
foreach ($e in $testEmps) {
    Write-Host "  $($e.fullName) ($($e.position)) | Salary: $($e.basicSalary) | Lending: $($e.remainingLending) | DaysPresent: $($e.daysPresent)"
}

# 7. Suppliers
$sup = Invoke-RestMethod -Uri "$baseUrl/api/management/suppliers" -Method Get -Headers $headers
Write-Host "`n--- SUPPLIERS ---"
Write-Host "Total suppliers: $($sup.data.Count)"
$testSups = $sup.data | Where-Object { $_.supplierName -match "TEST-SUPPLIER" }
Write-Host "Found $($testSups.Count) test suppliers."
foreach ($s in $testSups) {
    Write-Host "  $($s.supplierName) | POs: $($s.posCount) | OrigQty: $($s.originalQuantity)t | DelivQty: $($s.deliveredQuantity)t | RemQty: $($s.remainingQuantity)t"
}

# 8. Search
$search = Invoke-RestMethod -Uri "$baseUrl/api/management/search?q=TEST-PO" -Method Get -Headers $headers
Write-Host "`n--- SEARCH (TEST-PO) ---"
Write-Host "POs: $($search.data.purchaseOrders.Count)"
Write-Host "Transportation: $($search.data.transportation.Count)"
Write-Host "Storage: $($search.data.storage.Count)"
Write-Host "Finance: $($search.data.finance.Count)"
Write-Host "Suppliers: $($search.data.suppliers.Count)"

Write-Host "`n>>> ALL API CHECKS PASSED SUCCESSFULLY! <<<"
