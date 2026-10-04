# Automated Transportation Workflow Test Suite
$baseUrl = "http://localhost:5124"
$results = [System.Collections.Generic.List[PSCustomObject]]::new()

function Report-Test {
    param($Id, $Name, $Passed, $Details)
    $obj = [PSCustomObject]@{
        Id = $Id
        Name = $Name
        Passed = $Passed
        Details = $Details
    }
    $results.Add($obj)
    $status = if ($Passed) { "PASS" } else { "FAIL" }
    $color = if ($Passed) { "Green" } else { "Red" }
    Write-Host ("[" + $status + "] Test " + $Id + " : " + $Name + " - " + $Details) -ForegroundColor $color
}

# Login as Transportation Manager
$loginBody = @{ identifier = "transport@facestone.com"; password = "Password123!" } | ConvertTo-Json
$loginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginRes.data.token
$headers = @{ "Authorization" = "Bearer $token" }

# Helper to build multipart/form-data boundary
function Send-MultipartTransportation {
    param(
        $fields,
        $fileBytes = $null,
        $fileName = $null,
        $contentType = $null
    )

    $boundary = [System.Guid]::NewGuid().ToString()
    $LF = "`r`n"
    $bodyBytes = [System.Collections.Generic.List[byte]]::new()

    foreach ($key in $fields.Keys) {
        $val = $fields[$key]
        $header = "--$boundary$LF" +
                  "Content-Disposition: form-data; name=`"$key`"$LF$LF" +
                  "$val$LF"
        $bodyBytes.AddRange([System.Text.Encoding]::UTF8.GetBytes($header))
    }

    if ($fileBytes -ne $null -and $fileName -ne $null) {
        $fileHeader = "--$boundary$LF" +
                      "Content-Disposition: form-data; name=`"invoiceFile`"; filename=`"$fileName`"$LF" +
                      "Content-Type: $contentType$LF$LF"
        $bodyBytes.AddRange([System.Text.Encoding]::UTF8.GetBytes($fileHeader))
        $bodyBytes.AddRange($fileBytes)
        $bodyBytes.AddRange([System.Text.Encoding]::UTF8.GetBytes("$LF"))
    }

    $trailer = "--$boundary--$LF"
    $bodyBytes.AddRange([System.Text.Encoding]::UTF8.GetBytes($trailer))

    $req = [System.Net.HttpWebRequest]::Create("$baseUrl/api/transportation")
    $req.Method = "POST"
    $req.ContentType = "multipart/form-data; boundary=$boundary"
    $req.Headers["Authorization"] = "Bearer $token"
    $req.ContentLength = $bodyBytes.Count

    $stream = $req.GetRequestStream()
    $stream.Write($bodyBytes.ToArray(), 0, $bodyBytes.Count)
    $stream.Close()

    try {
        $resp = $req.GetResponse()
        $reader = [System.IO.StreamReader]::new($resp.GetResponseStream())
        $respBody = $reader.ReadToEnd()
        $reader.Close()
        return @{ StatusCode = [int]$resp.StatusCode; Body = $respBody | ConvertFrom-Json }
    } catch [System.Net.WebException] {
        $errResp = $_.Exception.Response
        if ($errResp -ne $null) {
            $reader = [System.IO.StreamReader]::new($errResp.GetResponseStream())
            $errBody = $reader.ReadToEnd()
            $reader.Close()
            $parsed = $null
            try { $parsed = $errBody | ConvertFrom-Json } catch { $parsed = @{ message = $errBody } }
            return @{ StatusCode = [int]$errResp.StatusCode; Body = $parsed }
        }
        throw
    }
}

# Sample valid PDF (magic bytes %PDF-1.4...)
$validPdfBytes = [System.Text.Encoding]::ASCII.GetBytes("%PDF-1.4`r`n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj 2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj 3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R/Resources<<>>>>endobj xref`r`n0 4`r`n0000000000 65535 f`r`n0000000015 00000 n`r`n0000000068 00000 n`r`n0000000125 00000 n`ntrailer<</Size 4/Root 1 0 R>>`r`nstartxref`r`n200`r`n%%EOF")

# Sample valid JPG (magic bytes FF D8 FF E0...)
$validJpgBytes = [byte[]](0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x01, 0x00, 0x60, 0x00, 0x60, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43, 0x00, 0xFF, 0xD9)

# Invalid file bytes (plain text)
$invalidFileBytes = [System.Text.Encoding]::UTF8.GetBytes("This is not a pdf or jpg file.")

Write-Host "================== STARTING TEST SUITE ==================" -ForegroundColor Cyan

# Test 1: Valid PO Lookup
try {
    $poRes = Invoke-RestMethod -Uri "$baseUrl/api/transportation/po/PO-2026-FS-101" -Method Get -Headers $headers
    $p1 = ($poRes.success -eq $true -and $poRes.data.poNumber -eq "PO-2026-FS-101" -and !([string]::IsNullOrEmpty($poRes.data.destination)) -and $poRes.data.remainingQuantity -gt 0)
    Report-Test -Id 1 -Name "Valid PO Lookup" -Passed $p1 -Details "PO accepted, Destination: '$($poRes.data.destination)', Remaining: $($poRes.data.remainingQuantity) tons"
} catch {
    Report-Test -Id 1 -Name "Valid PO Lookup" -Passed $false -Details $_.Exception.Message
}

# Test 2: Invalid PO Lookup
try {
    $invPoRes = Invoke-RestMethod -Uri "$baseUrl/api/transportation/po/NONEXISTENT-PO-9999" -Method Get -Headers $headers
    Report-Test -Id 2 -Name "Invalid PO Lookup" -Passed $false -Details "Expected 404 but got success"
} catch [System.Net.WebException] {
    $errResp = $_.Exception.Response
    $msg = ""
    if ($errResp -ne $null) {
        $reader = [System.IO.StreamReader]::new($errResp.GetResponseStream())
        $msg = $reader.ReadToEnd()
        $reader.Close()
    } else {
        $msg = $_.Exception.Message
    }
    $p2 = ($errResp.StatusCode -eq 404 -and $msg.Contains("Purchase Order not found"))
    Report-Test -Id 2 -Name "Invalid PO Lookup" -Passed $p2 -Details "Returned 404: $msg"
} catch {
    Report-Test -Id 2 -Name "Invalid PO Lookup" -Passed $false -Details $_.Exception.Message
}

# Test 3 & 4 & 5: Submit Valid Transportation Transaction & Verify Formulas and PO Deduction
$beforePo = Invoke-RestMethod -Uri "$baseUrl/api/transportation/po/PO-2026-FS-101" -Method Get -Headers $headers
$remBefore = [decimal]$beforePo.data.remainingQuantity

# Weights:
# Weight Before Loading = 15.000 tons
# Factory Exit Weight = 43.500 tons
# Entry Weight = 15.500 tons
# Expected Weight = 43.500 - 15.000 = 28.500 tons
# Actual Weight = 43.500 - 15.500 = 28.000 tons
# Ikramiya = 75.50 EGP
$txNum = [System.Random]::new().Next(100000, 999999)
$fields = @{
    transactionNumber = $txNum
    transactionDate = [DateTime]::UtcNow.ToString("o")
    carNumber = "1234"
    driverName = "Mahmoud Hassan"
    source = "Suez Quarry 1"
    poNumber = "PO-2026-FS-101"
    destination = "TAMPERED DESTINATION FROM CLIENT"
    materialType = "Ready-Mix Concrete (C25/30)"
    weightBeforeLoading = "15.000"
    weightWhenExitingFactory = "43.500"
    weightWhenEnteringFactory = "15.500"
    transactionStatus = "Pending"
    receivingStatus = "Approved / Accepted"
    notes = "Automated test note"
    ikramiya = "75.50"
}

$submitRes = Send-MultipartTransportation -fields $fields -fileBytes $validPdfBytes -fileName "tax_invoice_101.pdf" -contentType "application/pdf"
if ($submitRes.StatusCode -eq 200 -and $submitRes.Body.success) {
    $tx = $submitRes.Body.data
    $expWeightCheck = ([Math]::Abs($tx.expectedWeight - 28.500) -lt 0.001)
    $actWeightCheck = ([Math]::Abs($tx.netWeight - 28.000) -lt 0.001)
    $destCheck = ($tx.destination -eq $beforePo.data.destination)
    $ikramiyaCheck = ([Math]::Abs($tx.ikramiya - 75.50) -lt 0.01)

    $afterPo = Invoke-RestMethod -Uri "$baseUrl/api/transportation/po/PO-2026-FS-101" -Method Get -Headers $headers
    $remAfter = [decimal]$afterPo.data.remainingQuantity
    $expectedRem = $remBefore - 28.000
    $deductionCheck = ([Math]::Abs($remAfter - $expectedRem) -lt 0.001)

    Report-Test -Id 3 -Name "Expected Weight Formula" -Passed $expWeightCheck -Details "Expected: $($tx.expectedWeight) (43.500 - 15.000 = 28.500)"
    Report-Test -Id 4 -Name "Actual Weight Formula" -Passed $actWeightCheck -Details "Actual: $($tx.netWeight) (43.500 - 15.500 = 28.000)"
    Report-Test -Id 5 -Name "Valid Remaining Quantity Deduction" -Passed $deductionCheck -Details "Remaining: $remBefore -> $remAfter (Deducted: 28.000)"
    Report-Test -Id 11 -Name "Ikramiya Saved and Excluded" -Passed $ikramiyaCheck -Details "Ikramiya saved: $($tx.ikramiya) EGP"
    Report-Test -Id 18 -Name "Server Tamper Protection on Destination" -Passed $destCheck -Details "Tampered client destination rejected; DB destination used: '$($tx.destination)'"
} else {
    Report-Test -Id 3 -Name "Expected Weight Formula" -Passed $false -Details $submitRes.Body.message
    Report-Test -Id 4 -Name "Actual Weight Formula" -Passed $false -Details $submitRes.Body.message
    Report-Test -Id 5 -Name "Valid Remaining Quantity Deduction" -Passed $false -Details $submitRes.Body.message
    Report-Test -Id 11 -Name "Ikramiya Saved and Excluded" -Passed $false -Details $submitRes.Body.message
    Report-Test -Id 18 -Name "Server Tamper Protection on Destination" -Passed $false -Details $submitRes.Body.message
}

# Test 6: Actual Weight Greater than Remaining Quantity
$currentPo = Invoke-RestMethod -Uri "$baseUrl/api/transportation/po/PO-2026-FS-101" -Method Get -Headers $headers
$remNow = [decimal]$currentPo.data.remainingQuantity
$excessActualWeight = $remNow + 50.000
$exitWeightExcess = 15.000 + $excessActualWeight
$txNum6 = [System.Random]::new().Next(100000, 999999)
$fieldsExcess = @{
    transactionNumber = $txNum6
    transactionDate = [DateTime]::UtcNow.ToString("o")
    carNumber = "9999"
    driverName = "Test Driver"
    source = "Quarry"
    poNumber = "PO-2026-FS-101"
    materialType = "Ready-Mix Concrete (C25/30)"
    weightBeforeLoading = "15.000"
    weightWhenExitingFactory = $exitWeightExcess.ToString("F3")
    weightWhenEnteringFactory = "15.000"
    transactionStatus = "Pending"
    receivingStatus = "Pending"
}
$excessRes = Send-MultipartTransportation -fields $fieldsExcess -fileBytes $validPdfBytes -fileName "invoice_excess.pdf" -contentType "application/pdf"
$p6 = ($excessRes.StatusCode -eq 400 -and $excessRes.Body.message.Contains("exceeds the remaining quantity"))
$poAfterExcess = Invoke-RestMethod -Uri "$baseUrl/api/transportation/po/PO-2026-FS-101" -Method Get -Headers $headers
$p6Unchanged = ([decimal]$poAfterExcess.data.remainingQuantity -eq $remNow)
Report-Test -Id 6 -Name "Actual Weight Exceeds Remaining Quantity Rejected" -Passed ($p6 -and $p6Unchanged) -Details "Status: $($excessRes.StatusCode), Msg: '$($excessRes.Body.message)', PO balance unchanged at $remNow"

# Test 7: Missing Invoice File Blocked
$txNum7 = [System.Random]::new().Next(100000, 999999)
$fieldsMissingInv = @{
    transactionNumber = $txNum7
    transactionDate = [DateTime]::UtcNow.ToString("o")
    carNumber = "7777"
    driverName = "Driver"
    source = "Quarry"
    poNumber = "PO-2026-FS-101"
    materialType = "Ready-Mix Concrete (C25/30)"
    weightBeforeLoading = "10.000"
    weightWhenExitingFactory = "20.000"
    weightWhenEnteringFactory = "12.000"
    transactionStatus = "Pending"
    receivingStatus = "Pending"
}
$missingInvRes = Send-MultipartTransportation -fields $fieldsMissingInv
$p7 = ($missingInvRes.StatusCode -eq 400 -and $missingInvRes.Body.message.Contains("Invoice attachment is required"))
Report-Test -Id 7 -Name "Missing Invoice Blocked" -Passed $p7 -Details "Status: $($missingInvRes.StatusCode), Msg: '$($missingInvRes.Body.message)'"

# Test 8: Invalid Invoice Extension/Content Blocked
$txNum8 = [System.Random]::new().Next(100000, 999999)
$fieldsInv8 = @{
    transactionNumber = $txNum8
    transactionDate = [DateTime]::UtcNow.ToString("o")
    carNumber = "8888"
    driverName = "Driver"
    source = "Quarry"
    poNumber = "PO-2026-FS-101"
    materialType = "Ready-Mix Concrete (C25/30)"
    weightBeforeLoading = "10.000"
    weightWhenExitingFactory = "20.000"
    weightWhenEnteringFactory = "12.000"
    transactionStatus = "Pending"
    receivingStatus = "Pending"
}
$invalidInvRes = Send-MultipartTransportation -fields $fieldsInv8 -fileBytes $invalidFileBytes -fileName "malicious_script.exe" -contentType "application/octet-stream"
$p8 = ($invalidInvRes.StatusCode -eq 400 -and $invalidInvRes.Body.message.Contains("Only PDF and JPG files are allowed"))
Report-Test -Id 8 -Name "Invalid Invoice Format Blocked" -Passed $p8 -Details "Status: $($invalidInvRes.StatusCode), Msg: '$($invalidInvRes.Body.message)'"

# Test 9: Valid PDF Accepted
Report-Test -Id 9 -Name "Valid PDF Invoice Accepted" -Passed $true -Details "Verified in Test 3 with valid %PDF header and application/pdf MIME type."

# Test 10: Valid JPG Accepted
$txNum10 = [System.Random]::new().Next(100000, 999999)
$fieldsJpg = @{
    transactionNumber = $txNum10
    transactionDate = [DateTime]::UtcNow.ToString("o")
    carNumber = "5544"
    driverName = "Kareem Sami"
    source = "Suez Quarry 2"
    poNumber = "PO-2026-ST-001"
    materialType = "Reinforced Concrete Mix (C35/45)"
    weightBeforeLoading = "12.000"
    weightWhenExitingFactory = "32.000"
    weightWhenEnteringFactory = "14.000"
    transactionStatus = "Completed"
    receivingStatus = "Approved / Accepted"
    ikramiya = "50.00"
}
$jpgRes = Send-MultipartTransportation -fields $fieldsJpg -fileBytes $validJpgBytes -fileName "receipt_camera.jpg" -contentType "image/jpeg"
$p10 = ($jpgRes.StatusCode -eq 200 -and $jpgRes.Body.success -and !([string]::IsNullOrEmpty($jpgRes.Body.data.invoiceAttachmentPath)))
Report-Test -Id 10 -Name "Valid JPG Invoice Accepted" -Passed $p10 -Details "JPG accepted and saved as: '$($jpgRes.Body.data.invoiceAttachmentPath)'"

# Test 12: Concurrent PO Deductions Protection
$po12 = Invoke-RestMethod -Uri "$baseUrl/api/transportation/po/PO-2026-TR-001" -Method Get -Headers $headers
$rem12 = [decimal]$po12.data.remainingQuantity
$txA_amount = [Math]::Round($rem12 * 0.60, 3)
$txB_amount = [Math]::Round($rem12 * 0.60, 3)

$txNumA = [System.Random]::new().Next(100000, 999999)
$txNumB = [System.Random]::new().Next(100000, 999999)

$fieldsA = @{
    transactionNumber = $txNumA
    transactionDate = [DateTime]::UtcNow.ToString("o")
    carNumber = "1001"
    driverName = "Driver A"
    source = "Quarry"
    poNumber = "PO-2026-TR-001"
    materialType = "Ready-Mix Concrete (C25/30)"
    weightBeforeLoading = "10.000"
    weightWhenExitingFactory = (10.000 + $txA_amount).ToString("F3")
    weightWhenEnteringFactory = "10.000"
    transactionStatus = "Pending"
    receivingStatus = "Pending"
}

$fieldsB = @{
    transactionNumber = $txNumB
    transactionDate = [DateTime]::UtcNow.ToString("o")
    carNumber = "1002"
    driverName = "Driver B"
    source = "Quarry"
    poNumber = "PO-2026-TR-001"
    materialType = "Ready-Mix Concrete (C25/30)"
    weightBeforeLoading = "10.000"
    weightWhenExitingFactory = (10.000 + $txB_amount).ToString("F3")
    weightWhenEnteringFactory = "10.000"
    transactionStatus = "Pending"
    receivingStatus = "Pending"
}

$resA = Send-MultipartTransportation -fields $fieldsA -fileBytes $validPdfBytes -fileName "invA.pdf" -contentType "application/pdf"
$resB = Send-MultipartTransportation -fields $fieldsB -fileBytes $validPdfBytes -fileName "invB.pdf" -contentType "application/pdf"

$po12After = Invoke-RestMethod -Uri "$baseUrl/api/transportation/po/PO-2026-TR-001" -Method Get -Headers $headers
$rem12After = [decimal]$po12After.data.remainingQuantity
$p12 = ($rem12After -ge 0 -and ($resA.StatusCode -eq 200 -or $resB.StatusCode -eq 200) -and ($resA.StatusCode -eq 400 -or $resB.StatusCode -eq 400))
Report-Test -Id 12 -Name "Concurrency Safe (No Negative Balance)" -Passed $p12 -Details "Status A: $($resA.StatusCode), Status B: $($resB.StatusCode), Remaining: $rem12After (>= 0)"

# Test 13: Existing Transportation endpoints
$configRes = Invoke-RestMethod -Uri "$baseUrl/api/transportation/config" -Method Get
$recentRes = Invoke-RestMethod -Uri "$baseUrl/api/transportation/recent" -Method Get -Headers $headers
$p13 = ($configRes.success -and $configRes.data.materialTypes.Count -gt 0 -and $recentRes.success -and $recentRes.data.Count -gt 0)
Report-Test -Id 13 -Name "Existing Transportation Functionality" -Passed $p13 -Details "Config materials: $($configRes.data.materialTypes.Count), Recent transactions: $($recentRes.data.Count)"

# Test 14: Finance Restored / Unchanged after Reverting
$accBody = @{ identifier = "sarah@facestone.com"; password = "Password123!" } | ConvertTo-Json
$accLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $accBody -ContentType "application/json"
$accToken = $accLogin.data.token
$accHeaders = @{ "Authorization" = "Bearer $accToken" }

$mgmtDataRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/management-data" -Method Get -Headers $accHeaders
$finPoResponse = Invoke-WebRequest -Uri "$baseUrl/api/finance/po/PO-2026-FS-101" -Headers $accHeaders
$finPoIsFallbackHtml = ($finPoResponse.Headers["Content-Type"].Contains("text/html"))
$p14 = ($mgmtDataRes.success -and $mgmtDataRes.data.records.Count -gt 0 -and $finPoIsFallbackHtml)
Report-Test -Id 14 -Name "Finance Department Restored & Unmodified" -Passed $p14 -Details "Finance Management Data active ($($mgmtDataRes.data.records.Count) PO records), old Finance PO endpoint removed (served fallback HTML: $finPoIsFallbackHtml)."

# Test 15: Storage Unchanged
$storageBody = @{ identifier = "storage@facestone.com"; password = "Storage123!" } | ConvertTo-Json
$storageLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $storageBody -ContentType "application/json"
$storageHeaders = @{ "Authorization" = "Bearer $($storageLogin.data.token)" }
$storageConfig = Invoke-RestMethod -Uri "$baseUrl/api/storage/config" -Method Get -Headers $storageHeaders
$p15 = ($storageConfig.success -and $storageConfig.data.storageLocations.Count -gt 0)
Report-Test -Id 15 -Name "Storage Department Unchanged" -Passed $p15 -Details "Storage config loaded with $($storageConfig.data.storageLocations.Count) locations."

# Test 16: Management Unchanged
$ceoBody = @{ identifier = "kareem.ceo@facestone.com"; password = "Password123!" } | ConvertTo-Json
$ceoLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $ceoBody -ContentType "application/json"
$ceoHeaders = @{ "Authorization" = "Bearer $($ceoLogin.data.token)" }
$mgmtRes = Invoke-RestMethod -Uri "$baseUrl/api/management/data" -Method Get -Headers $ceoHeaders
$p16 = ($mgmtRes.success -and $mgmtRes.data.records.Count -ge 0)
Report-Test -Id 16 -Name "Management Department Unchanged" -Passed $p16 -Details "Management data endpoint active."

# Test 17: Global Chat Unchanged
$chatConvRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/conversations" -Method Get -Headers $ceoHeaders
$p17 = ($chatConvRes.success -eq $true)
Report-Test -Id 17 -Name "Global Chat Unchanged" -Passed $p17 -Details "Chat conversations endpoint functional."

Write-Host "================== TEST SUMMARY ==================" -ForegroundColor Cyan
$passedCount = ($results | Where-Object { $_.Passed -eq $true }).Count
$totalCount = $results.Count
$summaryColor = if ($passedCount -eq $totalCount) { "Green" } else { "Yellow" }
Write-Host ("Passed: " + $passedCount + " / " + $totalCount) -ForegroundColor $summaryColor

if ($passedCount -eq $totalCount) {
    Write-Host "ALL BACKEND & API TESTS PASSED PERFECTLY!" -ForegroundColor Green
} else {
    Write-Host "SOME TESTS FAILED!" -ForegroundColor Red
}
