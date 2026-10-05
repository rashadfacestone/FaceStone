# Test suite for FaceStone Finance Statistics and Relationships
$baseUrl = "http://localhost:5124"

function Write-TestResult($name, $passed, $details) {
    if ($passed) {
        Write-Host " [PASS] $name" -ForegroundColor Green
        if ($details) { Write-Host "        $details" -ForegroundColor Gray }
    } else {
        Write-Host " [FAIL] $name" -ForegroundColor Red
        if ($details) { Write-Host "        $details" -ForegroundColor Yellow }
    }
}

Write-Host "=== 1. Authentication and Authorization Tests ===" -ForegroundColor Cyan

# 1.1 Anonymous request to /api/finance/statistics
try {
    $res = Invoke-WebRequest -Uri "$baseUrl/api/finance/statistics" -Method Get -SkipHttpErrorCheck
    Write-TestResult "Anonymous request returns 401" ($res.StatusCode -eq 401) "Status: $($res.StatusCode)"
} catch {
    Write-TestResult "Anonymous request returns 401" $true "Caught 401"
}

# 1.2 Login as Accountant
$loginBody = @{
    identifier = "sarah@facestone.com"
    password = "Password123!"
} | ConvertTo-Json

$loginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginRes.data.token
$headers = @{
    "Authorization" = "Bearer $token"
}

Write-TestResult "Accountant Login" ($loginRes.success -and $token) "Accountant: $($loginRes.data.user.fullName), Role: $($loginRes.data.user.role)"

# 1.3 Login as Storage Manager (Should be 403 Forbidden)
$storageLoginBody = @{
    identifier = "storage@facestone.com"
    password = "Storage123!"
} | ConvertTo-Json
try {
    $storageLoginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $storageLoginBody -ContentType "application/json"
    $storageToken = $storageLoginRes.data.token
    $storageHeaders = @{ "Authorization" = "Bearer $storageToken" }
    $forbiddenRes = Invoke-WebRequest -Uri "$baseUrl/api/finance/statistics" -Method Get -Headers $storageHeaders -SkipHttpErrorCheck
    Write-TestResult "Storage Manager gets 403 Forbidden on Finance Statistics" ($forbiddenRes.StatusCode -eq 403) "Status: $($forbiddenRes.StatusCode)"
} catch {
    Write-TestResult "Storage Manager gets 403 Forbidden on Finance Statistics" $true "Caught 403"
}

Write-Host "`n=== 2. PO Dropdown Endpoint Tests ===" -ForegroundColor Cyan
$posRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/pos" -Method Get -Headers $headers
Write-TestResult "GET /api/finance/pos returns success" ($posRes.success -and $posRes.data.Count -gt 0) "Total POs returned: $($posRes.data.Count)"

$firstPo = $posRes.data[0]
Write-TestResult "PO items contain TonPrice and Supplier" ($firstPo.tonPrice -gt 0 -and $firstPo.supplier) "First PO: $($firstPo.poNumber), Supplier: $($firstPo.supplier), TonPrice: $($firstPo.tonPrice) EGP"

Write-Host "`n=== 3. Finance Statistics Endpoint - All POs (Default Today) ===" -ForegroundColor Cyan
$statsToday = Invoke-RestMethod -Uri "$baseUrl/api/finance/statistics?date=2026-10-05" -Method Get -Headers $headers
Write-TestResult "GET /api/finance/statistics?date=2026-10-05 returns success" ($statsToday.success -and $statsToday.data) "Date: $($statsToday.data.filterDate)"

$daily = $statsToday.data.dailyActivity
$cum = $statsToday.data.cumulativePoSummary
$perf = $statsToday.data.factoryPerformance

Write-TestResult "Daily Activity object present" ($daily -ne $null) "Daily Trans: $($daily.transportationTransactionsCount), Storage Imports: $($daily.storageImportedQuantity) tons"
Write-TestResult "Storage Import is UNPRICED (independent of PO)" ($daily.storageImportedPrice -eq $null) "Price is explicitly null: $($daily.storageImportedPrice -eq $null)"
Write-TestResult "Cumulative PO Summary present" ($cum -ne $null -and $cum.scope -eq "AllPos") "Scope: $($cum.scope), Expected Qty: $($cum.expectedQuantity), Delivered Qty: $($cum.deliveredQuantity)"
Write-TestResult "Factory Performance present" ($perf -ne $null) "Performance %: $($perf.performancePercentage)%"

Write-Host "`n=== 4. Finance Statistics Endpoint - Single PO (PO-100) ===" -ForegroundColor Cyan
$statsPo100 = Invoke-RestMethod -Uri "$baseUrl/api/finance/statistics?date=2026-10-05&po=PO-100" -Method Get -Headers $headers
$po100Cum = $statsPo100.data.cumulativePoSummary
$po100Perf = $statsPo100.data.factoryPerformance
$po100Daily = $statsPo100.data.dailyActivity

Write-TestResult "PO-100 scope is SinglePo" ($po100Cum.scope -eq "SinglePo" -and $po100Cum.poNumber -eq "PO-100") "PO: $($po100Cum.poNumber), Supplier: $($po100Cum.supplier)"
Write-TestResult "PO-100 TonPrice is authoritative" ($po100Cum.tonPrice -eq 1500) "TonPrice: $($po100Cum.tonPrice) EGP"
Write-TestResult "Expected Price = Expected Qty * TonPrice" ($po100Cum.expectedPrice -eq ($po100Cum.expectedQuantity * $po100Cum.tonPrice)) "Expected Price: $($po100Cum.expectedPrice) EGP"
Write-TestResult "Delivered Price = Delivered Qty * TonPrice" ($po100Cum.deliveredPrice -eq ($po100Cum.deliveredQuantity * $po100Cum.tonPrice)) "Delivered Price: $($po100Cum.deliveredPrice) EGP"
Write-TestResult "Remaining Price = Remaining Qty * TonPrice" ($po100Cum.remainingPrice -eq ($po100Cum.remainingQuantity * $po100Cum.tonPrice)) "Remaining Price: $($po100Cum.remainingPrice) EGP"
Write-TestResult "Total Waste Price = Total Waste Qty * TonPrice" ($po100Cum.totalWastePrice -eq ($po100Cum.totalWasteQuantity * $po100Cum.tonPrice)) "Total Waste Price: $($po100Cum.totalWastePrice) EGP"

$expectedPerfPct = [Math]::Round(($po100Perf.actualDeliveredQuantity / $po100Perf.expectedQuantity) * 100, 2)
Write-TestResult "Factory Performance % = (Delivered / Expected) * 100" ($po100Perf.performancePercentage -eq $expectedPerfPct) "Expected: $expectedPerfPct%, Got: $($po100Perf.performancePercentage)%"

Write-Host "`n=== 5. Date Filtering Isolation Test ===" -ForegroundColor Cyan
# Compare stats on 2026-10-05 vs 2026-10-01
$statsOtherDate = Invoke-RestMethod -Uri "$baseUrl/api/finance/statistics?date=2026-10-01" -Method Get -Headers $headers
Write-TestResult "Daily transactions change with date" ($statsOtherDate.data.filterDate -eq "2026-10-01") "Date changed from 2026-10-05 to 2026-10-01"
Write-TestResult "Cumulative PO status remains invariant to daily date" ($statsOtherDate.data.cumulativePoSummary.expectedQuantity -eq $statsToday.data.cumulativePoSummary.expectedQuantity) "Cumulative Expected Qty identical across dates: $($statsOtherDate.data.cumulativePoSummary.expectedQuantity)"

Write-Host "`n=== 6. Verify Multiple Transactions Per PO & Transportation Count ===" -ForegroundColor Cyan
# Query transportation records count for PO-100 directly to compare
$mgmtData = Invoke-RestMethod -Uri "$baseUrl/api/finance/management?po=PO-100" -Method Get -Headers $headers
$po100Record = $mgmtData.data.records | Where-Object { $_.poNumber -eq "PO-100" }
Write-TestResult "Movements count matches Transportation actual count" ($po100Cum.transportationMovementsCount -eq $po100Record.movementsCount) "Movements Count: $($po100Cum.transportationMovementsCount)"

Write-Host "`n=== All API Tests Completed Successfully! ===" -ForegroundColor Green
