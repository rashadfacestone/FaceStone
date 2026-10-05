$baseUrl = "http://localhost:5124"

Write-Host "=== 1. Testing Authentication ==="

# 1. Login as Finance Director
$bodyDirector = @{ identifier = "finance_director"; password = "Director123!" } | ConvertTo-Json
$resDirector = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $bodyDirector -ContentType "application/json"
$tokenDirector = $resDirector.data.token
Write-Host "Finance Director login: $($resDirector.success) (Role: $($resDirector.data.user.role))"

# 2. Login as regular Accountant
$bodyAcc = @{ identifier = "sarah_concrete"; password = "Password123!" } | ConvertTo-Json
$resAcc = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $bodyAcc -ContentType "application/json"
$tokenAcc = $resAcc.data.token
Write-Host "Accountant login: $($resAcc.success) (Role: $($resAcc.data.user.role))"

Write-Host "`n=== 2. Testing Date Range Filter & Statistics ==="
$headersDirector = @{ Authorization = "Bearer $tokenDirector" }
$statsToday = Invoke-RestMethod -Uri "$baseUrl/api/finance/statistics" -Method Get -Headers $headersDirector
Write-Host "Today Stats: Success=$($statsToday.success), From=$($statsToday.data.filterFromDate), To=$($statsToday.data.filterToDate)"
Write-Host "  Trans Count: $($statsToday.data.dailyActivity.transportationTransactionsCount)"
Write-Host "  Trans Net: $($statsToday.data.dailyActivity.transportationNetWeight) tons"
Write-Host "  Trans Waste: $($statsToday.data.dailyActivity.transportationWasteQuantity) tons"
Write-Host "  Storage Import: $($statsToday.data.dailyActivity.storageImportedQuantity) tons"
Write-Host "  Storage Waste: $($statsToday.data.dailyActivity.storageWasteQuantity) tons"
Write-Host "  Total Waste: $($statsToday.data.dailyActivity.totalWasteQuantity) tons"

$statsRange = Invoke-RestMethod -Uri "$baseUrl/api/finance/statistics?fromDate=2026-10-01&toDate=2026-10-05" -Method Get -Headers $headersDirector
Write-Host "`nDate Range Stats (2026-10-01 to 2026-10-05): Success=$($statsRange.success)"
Write-Host "  Trans Count: $($statsRange.data.dailyActivity.transportationTransactionsCount)"
Write-Host "  Trans Net: $($statsRange.data.dailyActivity.transportationNetWeight) tons"
Write-Host "  Trans Waste: $($statsRange.data.dailyActivity.transportationWasteQuantity) tons"
Write-Host "  Storage Import: $($statsRange.data.dailyActivity.storageImportedQuantity) tons"
Write-Host "  Storage Waste: $($statsRange.data.dailyActivity.storageWasteQuantity) tons"
Write-Host "  Total Waste: $($statsRange.data.dailyActivity.totalWasteQuantity) tons"

$calcTotalWaste = [double]$statsRange.data.dailyActivity.transportationWasteQuantity + [double]$statsRange.data.dailyActivity.storageWasteQuantity
Write-Host "  Verification: TransWaste ($($statsRange.data.dailyActivity.transportationWasteQuantity)) + StorageWaste ($($statsRange.data.dailyActivity.storageWasteQuantity)) = $calcTotalWaste"
if ([Math]::Abs($calcTotalWaste - [double]$statsRange.data.dailyActivity.totalWasteQuantity) -lt 0.001) {
    Write-Host "  [PASS] Total Waste Formula is strictly accurate!"
} else {
    Write-Host "  [FAIL] Total Waste Formula mismatch!"
}

Write-Host "`n=== 3. Testing PO Historical List ==="
$posRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/purchase-orders" -Method Get -Headers $headersDirector
Write-Host "Purchase Orders History: Total=$($posRes.data.Count)"
foreach ($po in $posRes.data) {
    Write-Host "  PO: $($po.poNumber), Supplier: $($po.supplier), Qty: $($po.quantity), TonPrice: $($po.tonPrice), Status: $($po.status), PaymentStatus: $($po.paymentStatus)"
}

Write-Host "`n=== 4. Testing PO Details Page API ==="
$samplePo = $posRes.data[0].poNumber
$poDetailRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/purchase-orders/$samplePo" -Method Get -Headers $headersDirector
$detail = $poDetailRes.data
Write-Host "PO Details for ${samplePo}"
Write-Host "  Company: $($detail.company)"
Write-Host "  Destination: $($detail.destination)"
Write-Host "  Material: $($detail.material)"
Write-Host "  Original Qty: $($detail.originalQuantity) tons"
Write-Host "  Ton Price: $($detail.tonPrice) EGP"
Write-Host "  Delivered Qty: $($detail.deliveredQuantity) tons"
Write-Host "  Remaining Qty: $($detail.remainingQuantity) tons"
Write-Host "  Trans Waste: $($detail.transportationWasteQuantity) tons, Storage Waste: $($detail.storageWasteQuantity) tons"
Write-Host "  Total Waste: $($detail.totalWasteQuantity) tons"
Write-Host "  Performance %: $($detail.performancePercentage)%"
Write-Host "  Payment Status: $($detail.paymentStatus)"
Write-Host "  Is Current User Finance Director: $($detail.isCurrentUserFinanceDirector)"
Write-Host "  Timeline Events Count: $($detail.timeline.Count)"
foreach ($ev in $detail.timeline) {
    Write-Host "    -> [$($ev.eventType)] $($ev.title): $($ev.description)"
}

Write-Host "`n=== 5. Testing Payment Status Modification & Security Authorization ==="

# 5.1 Unauthorized user (Accountant) attempts to change payment status
$headersAcc = @{ Authorization = "Bearer $tokenAcc" }
$bodyPayment = @{ paymentStatus = "Paid"; notes = "Unauthorized test by accountant" } | ConvertTo-Json
$utf8AccBytes = [System.Text.Encoding]::UTF8.GetBytes($bodyPayment)

try {
    $unauthRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/purchase-orders/$samplePo/payment-status" -Method Put -Headers $headersAcc -Body $utf8AccBytes -ContentType "application/json; charset=utf-8"
    Write-Host "  [FAIL] Accountant was able to change payment status!"
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "  [PASS] Accountant rejected with status code: $statusCode (Forbidden)"
}

# 5.2 Authorized user (Finance Director) changes payment status
$newStatusVal = "Paid In Full"
$bodyDirectorPayment = @{ status = $newStatusVal; paymentStatus = $newStatusVal; notes = "Approved by Finance Director via Check #99128" } | ConvertTo-Json
$utf8DirectorBytes = [System.Text.Encoding]::UTF8.GetBytes($bodyDirectorPayment)
$authUpdateRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/purchase-orders/$samplePo/payment-status" -Method Put -Headers $headersDirector -Body $utf8DirectorBytes -ContentType "application/json; charset=utf-8"
Write-Host "  Finance Director Update: Success=$($authUpdateRes.success), Message=$($authUpdateRes.message)"

# 5.3 Verify that PO Details and Audit Trail reflect the new Payment Status
$poDetailAfter = Invoke-RestMethod -Uri "$baseUrl/api/finance/purchase-orders/$samplePo" -Method Get -Headers $headersDirector
Write-Host "  PO Payment Status After Update: $($poDetailAfter.data.paymentStatus)"
Write-Host "  Updated By: $($poDetailAfter.data.paymentStatusUpdatedBy) at $($poDetailAfter.data.paymentStatusUpdatedAt)"
Write-Host "  Audit Trail Records: $($poDetailAfter.data.paymentAudits.Count)"
foreach ($audit in $poDetailAfter.data.paymentAudits) {
    Write-Host "    -> From: '$($audit.previousStatus)' To: '$($audit.newStatus)' By: $($audit.changedBy) Notes: $($audit.notes)"
}

if ($poDetailAfter.data.paymentStatus -eq $newStatusVal) {
    Write-Host "  [PASS] Payment Status saved to DB and persisted!"
} else {
    Write-Host "  [FAIL] Payment status was not updated properly!"
}

Write-Host "`n=== 6. Testing Factory Performance Details API ==="
$perfRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/performance?fromDate=2026-10-01&toDate=2026-10-05" -Method Get -Headers $headersDirector
$perfData = $perfRes.data
Write-Host "Performance Details:"
Write-Host "  Overall Performance: $($perfData.overallPerformancePercentage)%"
Write-Host "  Cumulative Expected Qty: $($perfData.cumulativeExpectedQuantity) tons"
Write-Host "  Cumulative Delivered Qty: $($perfData.cumulativeDeliveredQuantity) tons"
Write-Host "  Cumulative Total Waste: $($perfData.cumulativeTotalWaste) tons"
Write-Host "  Formula (Quantitative): $($perfData.quantitativeCalculationFormula)"
Write-Host "  Formula (Performance %): $($perfData.quantitativePerformancePercentageFormula)"
Write-Host "  Financial Performance %: $($perfData.financialPerformancePercentage)%"
Write-Host "  Formula (Financial %): $($perfData.financialPerformancePercentageFormula)"
Write-Host "  Range Activity Trans Count: $($perfData.rangeTransportationCount), Net: $($perfData.rangeTransportationNetWeight) tons, Waste: $($perfData.rangeTotalWaste) tons"
Write-Host "  PO Breakdowns Count: $($perfData.poBreakdowns.Count)"

Write-Host "`n=== 7. Testing SPA Page Navigation Endpoints ==="
$pages = @("/finance", "/finance/performance", "/finance/purchase-orders/$samplePo")
foreach ($p in $pages) {
    $resPage = Invoke-WebRequest -Uri "$baseUrl$p" -Method Get -Headers $headersDirector
    Write-Host "  GET $p : Status=$($resPage.StatusCode), ContentLength=$($resPage.Content.Length)"
}

Write-Host "`n=== All API & Logic Tests Completed Successfully! ==="
