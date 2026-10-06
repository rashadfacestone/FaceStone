$ErrorActionPreference = 'Stop'

# 1. Login as CEO
$loginBody = @{ identifier = 'kareem.ceo@facestone.com'; password = 'Password123!' } | ConvertTo-Json
$loginRes = Invoke-RestMethod -Uri 'http://localhost:5124/api/auth/login' -Method Post -Body $loginBody -ContentType 'application/json'
$token = $loginRes.data.token
$headers = @{ Authorization = "Bearer $token" }

Write-Host "=== 1. OVERVIEW ==="
$overview = Invoke-RestMethod -Uri 'http://localhost:5124/api/management/overview' -Method Get -Headers $headers
$overview | ConvertTo-Json -Depth 4

Write-Host "`n=== 2. SEARCH PO-100 ==="
$search1 = Invoke-RestMethod -Uri 'http://localhost:5124/api/management/search?q=PO-100' -Method Get -Headers $headers
$search1.data | ConvertTo-Json -Depth 3

Write-Host "`n=== 3. SEARCH AHMED ==="
$search2 = Invoke-RestMethod -Uri 'http://localhost:5124/api/management/search?q=Ahmed' -Method Get -Headers $headers
$search2.data | ConvertTo-Json -Depth 3

Write-Host "`n=== 4. SEARCH ARABIC محجر ==="
$search3 = Invoke-RestMethod -Uri 'http://localhost:5124/api/management/search?q=محجر' -Method Get -Headers $headers
$search3.data | ConvertTo-Json -Depth 3

Write-Host "`n=== 5. SEARCH MATERIAL powder ==="
$search4 = Invoke-RestMethod -Uri 'http://localhost:5124/api/management/search?q=powder' -Method Get -Headers $headers
$search4.data | ConvertTo-Json -Depth 3

Write-Host "`n=== 6. TRANSPORTATION OVERVIEW ==="
$trans = Invoke-RestMethod -Uri 'http://localhost:5124/api/management/transportation' -Method Get -Headers $headers
Write-Host "Total Trips: $($trans.data.totalTrips), Total Qty: $($trans.data.totalGrossWeight), Net: $($trans.data.totalNetWeight), Waste: $($trans.data.totalWasteWeight), Cars: $($trans.data.distinctCars), Drivers: $($trans.data.distinctDrivers)"

Write-Host "`n=== 7. STORAGE OVERVIEW ==="
$storage = Invoke-RestMethod -Uri 'http://localhost:5124/api/management/storage' -Method Get -Headers $headers
Write-Host "Deposits: $($storage.data.totalDeposits), Issues: $($storage.data.totalIssues), Balance: $($storage.data.currentBalance), Waste: $($storage.data.wasteQuantity), TxCount: $($storage.data.totalTransactionsCount)"

Write-Host "`n=== 8. FINANCE OVERVIEW ==="
$fin = Invoke-RestMethod -Uri 'http://localhost:5124/api/management/finance' -Method Get -Headers $headers
Write-Host "Total PO Val: $($fin.data.totalPoValue), Delivered Val: $($fin.data.deliveredValue), Rem Val: $($fin.data.remainingValue), Expenses: $($fin.data.totalExpenses), Perf: $($fin.data.factoryPerformancePercentage)"

Write-Host "`n=== 9. EMPLOYEES OVERVIEW ==="
$emp = Invoke-RestMethod -Uri 'http://localhost:5124/api/management/employees' -Method Get -Headers $headers
Write-Host "Total Emp: $($emp.data.totalEmployees), Salaried: $($emp.data.employeesWithSalaryCount), Total Lending: $($emp.data.totalLendingAmount)"
$emp.data.employees | Select-Object -First 3 | ConvertTo-Json

Write-Host "`n=== 10. SUPPLIERS OVERVIEW ==="
$sup = Invoke-RestMethod -Uri 'http://localhost:5124/api/management/suppliers' -Method Get -Headers $headers
Write-Host "Total Sup: $($sup.data.totalSuppliers), POs: $($sup.data.totalPurchaseOrders), Contracted: $($sup.data.totalContractedQuantity), Delivered: $($sup.data.totalDeliveredQuantity), Rem: $($sup.data.totalRemainingQuantity), Progress: $($sup.data.overallProgressPercentage)%"
$firstSupId = $sup.data.suppliers[0].supplierId
Write-Host "Testing supplier details for: $firstSupId"
$supDetail = Invoke-RestMethod -Uri "http://localhost:5124/api/management/suppliers/$firstSupId" -Method Get -Headers $headers
Write-Host "Supplier Name: $($supDetail.data.supplierName), POs: $($supDetail.data.purchaseOrdersCount), Trans: $($supDetail.data.transportationTransactions.Count), Storage: $($supDetail.data.storageTransactions.Count)"
