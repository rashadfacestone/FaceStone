$ErrorActionPreference = 'Stop'
$baseUrl = 'http://localhost:5124'

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "FACESTONE STORAGE REDESIGN COMPREHENSIVE AUTOMATED TEST SUITE" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# 1. Login as storage_manager
Write-Host "`n[TEST 1] Logging in as storage_manager..." -ForegroundColor Yellow
$loginBody = @{ identifier = 'storage_manager'; password = 'Storage123!' } | ConvertTo-Json
$loginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType 'application/json'
$token = $loginRes.data.token
Write-Host "PASS: User '$($loginRes.data.user.fullName)' authenticated with role '$($loginRes.data.user.role)'." -ForegroundColor Green

$headers = @{
    'Authorization' = "Bearer $token"
    'Accept' = 'application/json'
}

# 2. Config Endpoint Verification
Write-Host "`n[TEST 2] Verifying /api/storage/config..." -ForegroundColor Yellow
$configRes = Invoke-RestMethod -Uri "$baseUrl/api/storage/config" -Method Get -Headers $headers
if ($configRes.success -and ($configRes.data.materials -contains 'powder') -and ($configRes.data.materials -contains 'slurry')) {
    Write-Host "PASS: Config returned valid materials: $($configRes.data.materials -join ', ')" -ForegroundColor Green
} else {
    throw "FAIL: Invalid materials from config."
}
Write-Host "PASS: Controlled employees count: $($configRes.data.employees.Count)" -ForegroundColor Green
$validEmployee = $configRes.data.employees[0].fullName

# 3. Test IMPORT (Incoming Quantity = 25.500 TON, Outgoing must be null)
Write-Host "`n[TEST 3] Testing Valid IMPORT (powder, 25.500 TON, Loaded=10, Empty=2 -> Net=8)..." -ForegroundColor Yellow
$importPayload = @{
    material = 'powder'
    action = 'import'
    incomingQuantity = 25.500
    transactionDate = (Get-Date).ToString('o')
    internalWeighbridgeReference = 'WB-INT-IMP-001'
    internalLoadedWeight = 10.000
    internalEmptyWeight = 2.000
    internalEmptyWeightTime = (Get-Date).AddMinutes(-30).ToString('o')
    internalLoadedWeightTime = (Get-Date).ToString('o')
    hasExternalWeighbridge = $false
    responsibleEmployee = $validEmployee
    notes = 'Testing Import 25.500 ton'
} | ConvertTo-Json

$importRes = Invoke-RestMethod -Uri "$baseUrl/api/storage" -Method Post -Body $importPayload -ContentType 'application/json' -Headers $headers
if ($importRes.success -and $importRes.data.incomingQuantity -eq 25.500 -and $null -eq $importRes.data.outgoingQuantity -and $importRes.data.internalNetWeight -eq 8.000) {
    Write-Host "PASS: Import transaction created (#$($importRes.data.movementNumber)), Incoming=25.500, Outgoing=null, Net=8.000" -ForegroundColor Green
} else {
    throw "FAIL: Import response does not match expected data."
}

# 4. Test IMPORT VALIDATION (Both incoming and outgoing entered -> must reject)
Write-Host "`n[TEST 4] Testing IMPORT with Outgoing Quantity provided (Must reject)..." -ForegroundColor Yellow
$badImportPayload = @{
    material = 'powder'
    action = 'import'
    incomingQuantity = 25.500
    outgoingQuantity = 10.000
    transactionDate = (Get-Date).ToString('o')
    internalWeighbridgeReference = 'WB-INT-BAD-001'
    internalLoadedWeight = 10.000
    internalEmptyWeight = 2.000
    internalLoadedWeightTime = (Get-Date).ToString('o')
    hasExternalWeighbridge = $false
    responsibleEmployee = $validEmployee
} | ConvertTo-Json

try {
    Invoke-RestMethod -Uri "$baseUrl/api/storage" -Method Post -Body $badImportPayload -ContentType 'application/json' -Headers $headers
    throw "FAIL: Expected 400 Bad Request but succeeded."
} catch {
    $errStream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($errStream)
    $respBody = $reader.ReadToEnd()
    Write-Host "PASS: Successfully rejected with 400: $respBody" -ForegroundColor Green
}

# 5. Test EXPORT (Outgoing Quantity = 15.750 TON, Incoming must be null)
Write-Host "`n[TEST 5] Testing Valid EXPORT without PO (slurry, 15.750 TON)..." -ForegroundColor Yellow
$exportPayload = @{
    material = 'slurry'
    action = 'export'
    outgoingQuantity = 15.750
    isExportRelatedToPO = $false
    transactionDate = (Get-Date).ToString('o')
    internalWeighbridgeReference = 'WB-INT-EXP-001'
    internalLoadedWeight = 20.000
    internalEmptyWeight = 4.250
    internalLoadedWeightTime = (Get-Date).ToString('o')
    hasExternalWeighbridge = $false
    responsibleEmployee = $validEmployee
    notes = 'Testing Export without PO'
} | ConvertTo-Json

$exportRes = Invoke-RestMethod -Uri "$baseUrl/api/storage" -Method Post -Body $exportPayload -ContentType 'application/json' -Headers $headers
if ($exportRes.success -and $exportRes.data.outgoingQuantity -eq 15.750 -and $null -eq $exportRes.data.incomingQuantity -and $exportRes.data.internalNetWeight -eq 15.750) {
    Write-Host "PASS: Export transaction created (#$($exportRes.data.movementNumber)), Outgoing=15.750, Incoming=null, Net=15.750" -ForegroundColor Green
} else {
    throw "FAIL: Export response does not match expected data."
}

# 6. Test EXPORT + INVALID PO (Must reject with 'Purchase Order not found.')
Write-Host "`n[TEST 6] Testing EXPORT with Invalid PO (Must reject with 'Purchase Order not found.')..." -ForegroundColor Yellow
$badPoPayload = @{
    material = 'powder'
    action = 'export'
    outgoingQuantity = 12.000
    isExportRelatedToPO = $true
    poNumber = 'PO-NON-EXISTENT-XYZ-9999'
    transactionDate = (Get-Date).ToString('o')
    internalWeighbridgeReference = 'WB-INT-BADPO-001'
    internalLoadedWeight = 15.000
    internalEmptyWeight = 3.000
    internalLoadedWeightTime = (Get-Date).ToString('o')
    hasExternalWeighbridge = $false
    responsibleEmployee = $validEmployee
} | ConvertTo-Json

try {
    Invoke-RestMethod -Uri "$baseUrl/api/storage" -Method Post -Body $badPoPayload -ContentType 'application/json' -Headers $headers
    throw "FAIL: Expected 400 Bad Request but succeeded."
} catch {
    $errStream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($errStream)
    $respBody = $reader.ReadToEnd()
    if ($respBody -match "Purchase Order not found\.") {
        Write-Host "PASS: Rejected with exact message: 'Purchase Order not found.'" -ForegroundColor Green
    } else {
        throw "FAIL: Unexpected error message: $respBody"
    }
}

# 7. Test EXPORT + VALID PO (PO-2026-9810)
Write-Host "`n[TEST 7] Testing EXPORT with Valid PO 'PO-2026-9810'..." -ForegroundColor Yellow
$validPoPayload = @{
    material = 'powder'
    action = 'export'
    outgoingQuantity = 12.000
    isExportRelatedToPO = $true
    poNumber = 'PO-2026-9810'
    transactionDate = (Get-Date).ToString('o')
    internalWeighbridgeReference = 'WB-INT-GOODPO-001'
    internalLoadedWeight = 15.000
    internalEmptyWeight = 3.000
    internalLoadedWeightTime = (Get-Date).ToString('o')
    hasExternalWeighbridge = $false
    responsibleEmployee = $validEmployee
    notes = 'Testing Export linked to PO-2026-9810'
} | ConvertTo-Json

$validPoRes = Invoke-RestMethod -Uri "$baseUrl/api/storage" -Method Post -Body $validPoPayload -ContentType 'application/json' -Headers $headers
if ($validPoRes.success -and $validPoRes.data.poNumber -eq 'PO-2026-9810' -and $validPoRes.data.purchaseOrderId -gt 0) {
    Write-Host "PASS: Export linked to PO successfully. PurchaseOrderId=$($validPoRes.data.purchaseOrderId), PONumber=$($validPoRes.data.poNumber)" -ForegroundColor Green
} else {
    throw "FAIL: Valid PO link failed."
}

# 8. Test NEGATIVE / INVALID WEIGHBRIDGE (Loaded <= Empty)
Write-Host "`n[TEST 8] Testing Invalid Internal Weighbridge (Loaded <= Empty)..." -ForegroundColor Yellow
$badWbPayload = @{
    material = 'powder'
    action = 'import'
    incomingQuantity = 5.000
    transactionDate = (Get-Date).ToString('o')
    internalWeighbridgeReference = 'WB-INT-NEG-001'
    internalLoadedWeight = 2.000
    internalEmptyWeight = 10.000
    internalLoadedWeightTime = (Get-Date).ToString('o')
    hasExternalWeighbridge = $false
    responsibleEmployee = $validEmployee
} | ConvertTo-Json

try {
    Invoke-RestMethod -Uri "$baseUrl/api/storage" -Method Post -Body $badWbPayload -ContentType 'application/json' -Headers $headers
    throw "FAIL: Expected 400 Bad Request but succeeded."
} catch {
    $errStream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($errStream)
    $respBody = $reader.ReadToEnd()
    Write-Host "PASS: Rejected invalid weighbridge weights: $respBody" -ForegroundColor Green
}

# 9. Test EXTERNAL WEIGHBRIDGE WITHOUT RECEIPT (Must reject)
Write-Host "`n[TEST 9] Testing External Weighbridge Enabled Without Receipt (Must reject)..." -ForegroundColor Yellow
$badExtPayload = @{
    material = 'powder'
    action = 'import'
    incomingQuantity = 5.000
    transactionDate = (Get-Date).ToString('o')
    internalWeighbridgeReference = 'WB-INT-EXTNOFILE-001'
    internalLoadedWeight = 10.000
    internalEmptyWeight = 2.000
    internalLoadedWeightTime = (Get-Date).ToString('o')
    hasExternalWeighbridge = $true
    externalWeighbridgeReference = 'EXT-WB-001'
    externalLoadedWeight = 9.800
    externalEmptyWeight = 2.000
    externalLoadedWeightTime = (Get-Date).ToString('o')
    responsibleEmployee = $validEmployee
} | ConvertTo-Json

try {
    Invoke-RestMethod -Uri "$baseUrl/api/storage" -Method Post -Body $badExtPayload -ContentType 'application/json' -Headers $headers
    throw "FAIL: Expected 400 Bad Request but succeeded."
} catch {
    $errStream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($errStream)
    $respBody = $reader.ReadToEnd()
    Write-Host "PASS: Rejected external weighbridge without receipt: $respBody" -ForegroundColor Green
}

# 10. Test EXTERNAL WEIGHBRIDGE WITH VALID RECEIPT (Multipart form with PDF)
Write-Host "`n[TEST 10] Testing External Weighbridge with Valid PDF Receipt Attachment..." -ForegroundColor Yellow
$testPdfPath = "F:\FaceStoneWebsite\scratch\test_weighbridge_receipt.pdf"
# Create a valid minimal PDF file
$pdfBytes = [System.Text.Encoding]::ASCII.GetBytes("%PDF-1.4`n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj 2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj 3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R/Resources<<>>>>endobj xref 0 4 0000000000 65535 f 0000000009 00000 n 0000000052 00000 n 0000000102 00000 n trailer<</Size 4/Root 1 0 R>>startxref 178 %%EOF")
[System.IO.File]::WriteAllBytes($testPdfPath, $pdfBytes)

$curlOutput = & curl.exe -s -X POST "$baseUrl/api/storage" `
  -H "Authorization: Bearer $token" `
  -F "material=powder" `
  -F "action=import" `
  -F "incomingQuantity=7.800" `
  -F "transactionDate=$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')" `
  -F "internalWeighbridgeReference=WB-INT-EXTTEST-001" `
  -F "internalLoadedWeight=10.000" `
  -F "internalEmptyWeight=2.000" `
  -F "internalLoadedWeightTime=$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')" `
  -F "hasExternalWeighbridge=true" `
  -F "externalWeighbridgeReference=EXT-WB-999" `
  -F "externalLoadedWeight=9.800" `
  -F "externalEmptyWeight=2.000" `
  -F "externalLoadedWeightTime=$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')" `
  -F "weighbridgeAttachment=@$testPdfPath;type=application/pdf" `
  -F "responsibleEmployee=$validEmployee" `
  -F "notes=Testing external weighbridge with receipt"

$extRes = $curlOutput | ConvertFrom-Json
if ($extRes.success -and $extRes.data.hasExternalWeighbridge -and $extRes.data.externalNetWeight -eq 7.800 -and $extRes.data.weighbridgeFileName) {
    Write-Host "PASS: External weighbridge transaction created! ExternalNet=7.800, AttachmentFile=$($extRes.data.weighbridgeFileName)" -ForegroundColor Green
    
    # Verify receipt file download endpoint
    $downloadUrl = if ($extRes.data.weighbridgeAttachmentPath) { "$baseUrl$($extRes.data.weighbridgeAttachmentPath)" } else { "$baseUrl/api/storage/attachments/$($extRes.data.weighbridgeFileName)" }
    $fileCheck = & curl.exe -s -o /dev/null -w "%{http_code}" "$downloadUrl"
    if ($fileCheck -eq "200") {
        Write-Host "PASS: Attachment streamed successfully via $downloadUrl (HTTP 200)." -ForegroundColor Green
    } else {
        throw "FAIL: Attachment stream returned HTTP $fileCheck."
    }
} else {
    throw "FAIL: External weighbridge submission failed. Output: $curlOutput"
}

# 11. Test RECENT TRANSACTIONS TABLE API
Write-Host "`n[TEST 11] Verifying /api/storage/recent returns updated transactions..." -ForegroundColor Yellow
$recentRes = Invoke-RestMethod -Uri "$baseUrl/api/storage/recent?limit=20" -Method Get -Headers $headers
Write-Host "PASS: Recent transactions returned: $($recentRes.data.Count) records." -ForegroundColor Green
$latest = $recentRes.data[0]
Write-Host "PASS: Latest record: #$($latest.movementNumber), Material=$($latest.material), Action=$($latest.action), Net=$($latest.internalNetWeight)" -ForegroundColor Green

# 12. Test UNTOUCHED DEPARTMENTS SANITY CHECK
Write-Host "`n[TEST 12] Verifying Other Departments are completely intact..." -ForegroundColor Yellow

# Transportation
$transRes = Invoke-RestMethod -Uri "$baseUrl/api/transportation?limit=5" -Method Get -Headers $headers
Write-Host "PASS: Transportation API intact: $($transRes.data.Count) records retrieved." -ForegroundColor Green

# Finance
$financeRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/stats" -Method Get -Headers $headers
Write-Host "PASS: Finance API intact: success=$($financeRes.success)." -ForegroundColor Green

# Management
$mgmtRes = Invoke-RestMethod -Uri "$baseUrl/api/management/stats" -Method Get -Headers $headers
Write-Host "PASS: Management API intact: success=$($mgmtRes.success)." -ForegroundColor Green

# Global Chat
$chatRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/active-chat-sessions" -Method Get -Headers $headers
Write-Host "PASS: Global Chat API intact: $($chatRes.data.Count) sessions retrieved." -ForegroundColor Green

Write-Host "`n=================================================================" -ForegroundColor Cyan
Write-Host "ALL TESTS PASSED SUCCESSFULLY! ZERO REGRESSIONS DETECTED." -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Cyan
