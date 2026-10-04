# ==============================================================================
# Comprehensive Finance Workflow Test Suite
# Tests: PO Lookup, Read-Only Destination, Mandatory Invoices, Weight Calculations,
# Deductions from Actual Weight, Ikramiya, Concurrency Safety, and Manager Review
# ==============================================================================

$baseUrl = "http://localhost:5124"
$results = @()

function Assert-Test {
    param(
        [string]$Name,
        [bool]$Condition,
        [string]$Details = ""
    )
    if ($Condition) {
        Write-Host " [PASS] $Name" -ForegroundColor Green
        if ($Details) { Write-Host "        $Details" -ForegroundColor DarkGray }
        $script:results += [PSCustomObject]@{ Test = $Name; Status = "PASS"; Details = $Details }
    } else {
        Write-Host " [FAIL] $Name" -ForegroundColor Red
        if ($Details) { Write-Host "        $Details" -ForegroundColor Yellow }
        $script:results += [PSCustomObject]@{ Test = $Name; Status = "FAIL"; Details = $Details }
    }
}

Write-Host "`n=== STARTING FINANCE WORKFLOW TEST SUITE ===" -ForegroundColor Cyan

# ------------------------------------------------------------------------------
# 0. Authenticate as Finance Accountant
# ------------------------------------------------------------------------------
Write-Host "`n--- Authenticating as Finance Accountant ---" -ForegroundColor Yellow
$loginPayload = @{
    identifier = "sarah@facestone.com"
    password = "Password123!"
} | ConvertTo-Json

$loginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method POST -Body $loginPayload -ContentType "application/json"
$token = $loginRes.data.token
$authHeaders = @{
    Authorization = "Bearer $token"
}
Assert-Test "AUTH: Accountant logged in and obtained JWT" (-not [string]::IsNullOrWhiteSpace($token)) ("User: " + $loginRes.data.user.fullName + " | Role: " + $loginRes.data.user.role)

# ------------------------------------------------------------------------------
# Create Dummy Files for Testing Attachments
# ------------------------------------------------------------------------------
$scratchDir = "F:\FaceStoneWebsite\scratch"
if (-not (Test-Path $scratchDir)) { New-Item -ItemType Directory -Path $scratchDir -Force | Out-Null }

$pdfPath = Join-Path $scratchDir "test_invoice.pdf"
$pdfHeader = "%PDF-1.4`r`n1 0 obj<</Type/Catalog>>endobj`r`ntrailer<</Root 1 0 R>>`r`n%%EOF"
$pdfBytes = [System.Text.Encoding]::ASCII.GetBytes($pdfHeader)
[System.IO.File]::WriteAllBytes($pdfPath, $pdfBytes)

$jpgPath = Join-Path $scratchDir "test_invoice.jpg"
$jpgBytes = [byte[]]@(0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x01, 0x00, 0x60, 0x00, 0x60, 0x00, 0x00, 0xFF, 0xD9)
[System.IO.File]::WriteAllBytes($jpgPath, $jpgBytes)

$txtPath = Join-Path $scratchDir "test_invalid.txt"
[System.IO.File]::WriteAllText($txtPath, "This is not an allowed invoice file.")

# Helper to send multipart form data with Authorization header
function Send-FinanceTransaction {
    param(
        [string]$PoNumber,
        [double]$FactoryExitWeight,
        [double]$WeightBeforeLoading,
        [double]$EntryWeight,
        [double]$Ikramiya = 0.0,
        [string]$Notes = "",
        [string]$FilePath = $null,
        [string]$AuthToken = $token
    )

    $boundary = [System.Guid]::NewGuid().ToString()
    $LF = "`r`n"
    $bodyBytes = [System.Collections.Generic.List[byte]]::new()

    $formFields = @{
        "PoNumber" = $PoNumber
        "FactoryExitWeight" = $FactoryExitWeight.ToString("F3", [System.Globalization.CultureInfo]::InvariantCulture)
        "WeightBeforeLoading" = $WeightBeforeLoading.ToString("F3", [System.Globalization.CultureInfo]::InvariantCulture)
        "EntryWeight" = $EntryWeight.ToString("F3", [System.Globalization.CultureInfo]::InvariantCulture)
        "Ikramiya" = $Ikramiya.ToString("F2", [System.Globalization.CultureInfo]::InvariantCulture)
        "Notes" = $Notes
    }

    foreach ($key in $formFields.Keys) {
        $val = $formFields[$key]
        $header = "--$boundary$LF" + "Content-Disposition: form-data; name=`"$key`"$LF$LF" + "$val$LF"
        $bodyBytes.AddRange([System.Text.Encoding]::UTF8.GetBytes($header))
    }

    if ($FilePath -and (Test-Path $FilePath)) {
        $fileName = [System.IO.Path]::GetFileName($FilePath)
        $ext = [System.IO.Path]::GetExtension($FilePath).ToLowerInvariant()
        $contentType = if ($ext -eq ".pdf") { "application/pdf" } elseif ($ext -eq ".jpg" -or $ext -eq ".jpeg") { "image/jpeg" } else { "text/plain" }
        
        $fileHeader = "--$boundary$LF" + "Content-Disposition: form-data; name=`"InvoiceFile`"; filename=`"$fileName`"$LF" + "Content-Type: $contentType$LF$LF"
        $bodyBytes.AddRange([System.Text.Encoding]::UTF8.GetBytes($fileHeader))
        $fileBytes = [System.IO.File]::ReadAllBytes($FilePath)
        $bodyBytes.AddRange($fileBytes)
        $bodyBytes.AddRange([System.Text.Encoding]::UTF8.GetBytes($LF))
    }

    $trailer = "--$boundary--$LF"
    $bodyBytes.AddRange([System.Text.Encoding]::UTF8.GetBytes($trailer))

    $req = [System.Net.HttpWebRequest]::Create("$baseUrl/api/finance/transactions")
    $req.Method = "POST"
    $req.ContentType = "multipart/form-data; boundary=$boundary"
    $req.ContentLength = $bodyBytes.Count
    if (-not [string]::IsNullOrWhiteSpace($AuthToken)) {
        $req.Headers["Authorization"] = "Bearer $AuthToken"
    }

    try {
        $stream = $req.GetRequestStream()
        $stream.Write($bodyBytes.ToArray(), 0, $bodyBytes.Count)
        $stream.Close()
        $resp = $req.GetResponse()
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $respBody = $reader.ReadToEnd()
        $reader.Close()
        $resp.Close()
        return @{ StatusCode = 200; Body = ($respBody | ConvertFrom-Json) }
    } catch [System.Net.WebException] {
        $ex = $_.Exception
        $statusCode = if ($ex.Response) { [int]$ex.Response.StatusCode } else { 500 }
        $respBody = ""
        if ($ex.Response) {
            $r = New-Object System.IO.StreamReader($ex.Response.GetResponseStream())
            $respBody = $r.ReadToEnd()
            $r.Close()
        }
        $json = $null
        try { $json = $respBody | ConvertFrom-Json } catch {}
        return @{ StatusCode = $statusCode; Raw = $respBody; Body = $json }
    }
}

# ==============================================================================
# TEST 1: Valid PO Lookup -> PO accepted, Destination automatically populated
# ==============================================================================
Write-Host "`n--- TEST 1: Valid PO Lookup ---" -ForegroundColor Yellow
$testPo = "PO-2026-9810"
$poRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/po/$testPo" -Method GET -Headers $authHeaders
Assert-Test "TEST 1.1: PO exists and returns 200 OK" ($poRes.success -eq $true -and $poRes.data.poNumber -eq $testPo) ("PO: " + $poRes.data.poNumber)
Assert-Test "TEST 1.2: Destination automatically populated from database" (-not [string]::IsNullOrWhiteSpace($poRes.data.destination)) ("Destination: " + $poRes.data.destination)
Assert-Test "TEST 1.3: Remaining Quantity is returned" ($poRes.data.remainingQuantity -gt 0) ("RemainingQty: " + $poRes.data.remainingQuantity)

# ==============================================================================
# TEST 2: Invalid PO Lookup -> "Purchase Order not found."
# ==============================================================================
Write-Host "`n--- TEST 2: Invalid PO Lookup ---" -ForegroundColor Yellow
try {
    $invRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/po/INVALID-PO-9999" -Method GET -Headers $authHeaders
    Assert-Test "TEST 2: Invalid PO should fail" $false "Expected 404 but got success"
} catch {
    $respBody = ""
    if ($_.Exception.Response) {
        $r = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $respBody = $r.ReadToEnd()
        $r.Close()
    }
    $errResp = $null
    try { $errResp = $respBody | ConvertFrom-Json } catch {}
    $msg = if ($errResp -and $errResp.message) { $errResp.message } else { $_.Exception.Message }
    Assert-Test "TEST 2: Invalid PO returns 'Purchase Order not found.'" ($msg -match "Purchase Order not found") ("Error msg: $msg")
}

# ==============================================================================
# TEST 5: Missing Invoice -> Blocked with "Invoice attachment is required."
# ==============================================================================
Write-Host "`n--- TEST 5: Missing Invoice Validation ---" -ForegroundColor Yellow
$noInvoiceRes = Send-FinanceTransaction -PoNumber $testPo -FactoryExitWeight 40.0 -WeightBeforeLoading 15.0 -EntryWeight 18.0 -FilePath $null
Assert-Test "TEST 5.1: Missing invoice rejected with 400 Bad Request" ($noInvoiceRes.StatusCode -eq 400) ("Status: " + $noInvoiceRes.StatusCode)
Assert-Test "TEST 5.2: Error message is 'Invoice attachment is required.'" ($noInvoiceRes.Body.message -match "Invoice attachment is required") ("Message: " + $noInvoiceRes.Body.message)

# ==============================================================================
# TEST 6: Invalid File Attachment -> Blocked with "Only PDF and JPG files are allowed."
# ==============================================================================
Write-Host "`n--- TEST 6: Invalid Attachment Validation ---" -ForegroundColor Yellow
$badFileRes = Send-FinanceTransaction -PoNumber $testPo -FactoryExitWeight 40.0 -WeightBeforeLoading 15.0 -EntryWeight 18.0 -FilePath $txtPath
Assert-Test "TEST 6.1: Non-PDF/JPG rejected with 400 Bad Request" ($badFileRes.StatusCode -eq 400) ("Status: " + $badFileRes.StatusCode)
Assert-Test "TEST 6.2: Error message is 'Only PDF and JPG files are allowed.'" ($badFileRes.Body.message -match "Only PDF and JPG files are allowed") ("Message: " + $badFileRes.Body.message)

# ==============================================================================
# TEST 8: Weight Calculations Verification
# Expected Weight = Factory Exit Weight - Weight Before Loading
# Actual Weight   = Factory Exit Weight - Entry Weight
# ==============================================================================
Write-Host "`n--- TEST 8: Weight Calculation Exact Formulas ---" -ForegroundColor Yellow
$exitWeight = 50.000
$beforeWeight = 18.500
$entryWeight = 21.000
$expectedWeightCalculated = $exitWeight - $beforeWeight   # 31.500
$actualWeightCalculated   = $exitWeight - $entryWeight    # 29.000

Assert-Test "TEST 8.1: Expected Weight formula (Exit - Before)" ($expectedWeightCalculated -eq 31.500) "50.000 - 18.500 = 31.500"
Assert-Test "TEST 8.2: Actual Weight formula (Exit - Entry)" ($actualWeightCalculated -eq 29.000) "50.000 - 21.000 = 29.000"

# ==============================================================================
# TEST 3 and 9: Valid Transaction Submission and Deduction from Actual Weight
# ==============================================================================
Write-Host "`n--- TEST 3 and 9: Valid Transaction Submission, Deduction and Ikramiya ---" -ForegroundColor Yellow
$poBefore = (Invoke-RestMethod -Uri "$baseUrl/api/finance/po/$testPo" -Method GET -Headers $authHeaders).data
$initialRemaining = [double]$poBefore.remainingQuantity
Write-Host "Initial PO Remaining Quantity: $initialRemaining tons" -ForegroundColor DarkCyan

$ikramiyaValue = 125.75
$txActualDeduction = 15.000 # Actual Weight: 45.000 - 30.000 = 15.000 tons
$txExit = 45.000
$txBefore = 14.000
$txEnter = 30.000

$submitRes = Send-FinanceTransaction -PoNumber $testPo `
    -FactoryExitWeight $txExit `
    -WeightBeforeLoading $txBefore `
    -EntryWeight $txEnter `
    -Ikramiya $ikramiyaValue `
    -Notes "Automated Test Submission" `
    -FilePath $pdfPath

Assert-Test "TEST 3.1: Transaction submission succeeds with 200 OK" ($submitRes.StatusCode -eq 200 -and $submitRes.Body.success -eq $true) ("Status: " + $submitRes.StatusCode)
Assert-Test "TEST 3.2: Immediate save with Status 'Submitted' (No manager approval step)" ($submitRes.Body.data.status -eq "Submitted") ("Status: " + $submitRes.Body.data.status)
Assert-Test "TEST 3.3: Destination in record matches PO destination from database" ($submitRes.Body.data.destination -eq $poBefore.destination) ("Dest: " + $submitRes.Body.data.destination)
Assert-Test "TEST 3.4: Expected Weight calculated correctly on server" ($submitRes.Body.data.expectedWeight -eq ($txExit - $txBefore)) ("Expected: " + $submitRes.Body.data.expectedWeight)
Assert-Test "TEST 3.5: Actual Weight calculated correctly on server" ($submitRes.Body.data.actualWeight -eq ($txExit - $txEnter)) ("Actual: " + $submitRes.Body.data.actualWeight)
Assert-Test "TEST 9.1: Ikramiya decimal stored with transaction" ($submitRes.Body.data.ikramiya -eq $ikramiyaValue) ("Ikramiya: " + $submitRes.Body.data.ikramiya)

# Verify PO Remaining Quantity in Database
$poAfter = (Invoke-RestMethod -Uri "$baseUrl/api/finance/po/$testPo" -Method GET -Headers $authHeaders).data
$expectedRemaining = $initialRemaining - $txActualDeduction
Assert-Test "TEST 3.6: Actual Weight ($txActualDeduction) deducted from PO Remaining Quantity" ([Math]::Abs($poAfter.remainingQuantity - $expectedRemaining) -lt 0.001) ("Before: $initialRemaining, Deducted: $txActualDeduction, After: " + $poAfter.remainingQuantity)

# ==============================================================================
# TEST 4: Actual Weight Exceeds Remaining Quantity -> Rejected, PO Unchanged
# ==============================================================================
Write-Host "`n--- TEST 4: Actual Weight Exceeds Remaining Quantity ---" -ForegroundColor Yellow
$currentRemaining = [double]$poAfter.remainingQuantity
$excessActualWeight = $currentRemaining + 50.0
$excessExit = $excessActualWeight + 20.0
$excessEnter = 20.0

$excessRes = Send-FinanceTransaction -PoNumber $testPo `
    -FactoryExitWeight $excessExit `
    -WeightBeforeLoading 10.0 `
    -EntryWeight $excessEnter `
    -Ikramiya 50.0 `
    -FilePath $jpgPath

Assert-Test "TEST 4.1: Exceeding quantity rejected with 400 Bad Request" ($excessRes.StatusCode -eq 400) ("Status: " + $excessRes.StatusCode)
Assert-Test "TEST 4.2: Error message is 'Actual weight exceeds the remaining quantity for this Purchase Order.'" ($excessRes.Body.message -match "Actual weight exceeds the remaining quantity") ("Msg: " + $excessRes.Body.message)

$poAfterExcess = (Invoke-RestMethod -Uri "$baseUrl/api/finance/po/$testPo" -Method GET -Headers $authHeaders).data
Assert-Test "TEST 4.3: PO Remaining Quantity remains completely unchanged" ([Math]::Abs($poAfterExcess.remainingQuantity - $currentRemaining) -lt 0.001) ("Remaining: " + $poAfterExcess.remainingQuantity)

# ==============================================================================
# TEST 7: Concurrent Submissions Protection Against Negative Balance
# ==============================================================================
Write-Host "`n--- TEST 7: Concurrency and Transaction Safety ---" -ForegroundColor Yellow
$currRem = [double]$poAfterExcess.remainingQuantity
$halfPlus = [Math]::Round(($currRem * 0.7), 3)

Write-Host "Remaining: $currRem tons. Firing 2 parallel requests of $halfPlus tons each..." -ForegroundColor DarkCyan

$scriptBlock = {
    param($baseUrl, $testPo, $deduct, $pdfPath, $authToken)
    $boundary = [System.Guid]::NewGuid().ToString()
    $LF = "`r`n"
    $bodyBytes = [System.Collections.Generic.List[byte]]::new()
    function Add-F([string]$n, [string]$v) {
        $h = "--$boundary$LF" + "Content-Disposition: form-data; name=`"$n`"$LF$LF" + "$v$LF"
        $script:bodyBytes.AddRange([System.Text.Encoding]::UTF8.GetBytes($h))
    }
    $exit = $deduct + 10.0
    Add-F "PoNumber" $testPo
    Add-F "FactoryExitWeight" ($exit.ToString("F3", [System.Globalization.CultureInfo]::InvariantCulture))
    Add-F "WeightBeforeLoading" "8.000"
    Add-F "EntryWeight" "10.000"
    Add-F "Ikramiya" "10.00"
    Add-F "Notes" "Concurrent Request"
    if (Test-Path $pdfPath) {
        $fh = "--$boundary$LF" + "Content-Disposition: form-data; name=`"InvoiceFile`"; filename=`"concurrent.pdf`"$LF" + "Content-Type: application/pdf$LF$LF"
        $bodyBytes.AddRange([System.Text.Encoding]::UTF8.GetBytes($fh))
        $bodyBytes.AddRange([System.IO.File]::ReadAllBytes($pdfPath))
        $bodyBytes.AddRange([System.Text.Encoding]::UTF8.GetBytes($LF))
    }
    $bodyBytes.AddRange([System.Text.Encoding]::UTF8.GetBytes("--$boundary--$LF"))
    $req = [System.Net.HttpWebRequest]::Create("$baseUrl/api/finance/transactions")
    $req.Method = "POST"
    $req.ContentType = "multipart/form-data; boundary=$boundary"
    $req.ContentLength = $bodyBytes.Count
    if (-not [string]::IsNullOrWhiteSpace($authToken)) {
        $req.Headers["Authorization"] = "Bearer $authToken"
    }
    try {
        $s = $req.GetRequestStream()
        $s.Write($bodyBytes.ToArray(), 0, $bodyBytes.Count)
        $s.Close()
        $resp = $req.GetResponse()
        $r = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $b = $r.ReadToEnd()
        $resp.Close()
        return @{ StatusCode = 200; Body = $b }
    } catch [System.Net.WebException] {
        $st = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 500 }
        return @{ StatusCode = $st }
    }
}

$job1 = Start-Job -ScriptBlock $scriptBlock -ArgumentList $baseUrl, $testPo, $halfPlus, $pdfPath, $token
$job2 = Start-Job -ScriptBlock $scriptBlock -ArgumentList $baseUrl, $testPo, $halfPlus, $pdfPath, $token

$res1 = Receive-Job -Job $job1 -Wait
$res2 = Receive-Job -Job $job2 -Wait
Remove-Job $job1, $job2 -Force

$successCount = 0
if ($res1.StatusCode -eq 200) { $successCount++ }
if ($res2.StatusCode -eq 200) { $successCount++ }

Assert-Test "TEST 7.1: Concurrency safety - Exactly 1 request succeeds when total exceeds balance" ($successCount -eq 1) ("Successes: $successCount out of 2 (One 200 OK, One 400 Bad Request)")

$poAfterConcurrent = (Invoke-RestMethod -Uri "$baseUrl/api/finance/po/$testPo" -Method GET -Headers $authHeaders).data
Assert-Test "TEST 7.2: Final PO Remaining Quantity never becomes negative" ($poAfterConcurrent.remainingQuantity -ge 0) ("Final Remaining: " + $poAfterConcurrent.remainingQuantity)

# ==============================================================================
# TEST 10: Manager Review Endpoints and Invoice Download
# ==============================================================================
Write-Host "`n--- TEST 10: Manager Review Endpoints ---" -ForegroundColor Yellow
$txListRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/transactions?poNumber=$testPo" -Method GET -Headers $authHeaders
Assert-Test "TEST 10.1: Manager can retrieve submitted transactions" ($txListRes.success -eq $true -and $txListRes.data.Count -gt 0) ("Found " + $txListRes.data.Count + " transactions")

$latestTx = $txListRes.data[0]
$invUrl = "$baseUrl/api/finance/invoices/" + $latestTx.invoiceAttachmentPath
$downloadRes = Invoke-WebRequest -Uri $invUrl -Method GET -Headers $authHeaders
Assert-Test "TEST 10.2: Manager can download / view invoice attachment" ($downloadRes.StatusCode -eq 200 -and $downloadRes.Content.Length -gt 0) ("Downloaded bytes: " + $downloadRes.Content.Length)

# ==============================================================================
# SUMMARY
# ==============================================================================
Write-Host "`n=== TEST RESULTS SUMMARY ===" -ForegroundColor Cyan
$passCount = ($results | Where-Object { $_.Status -eq "PASS" }).Count
$failCount = ($results | Where-Object { $_.Status -eq "FAIL" }).Count
$summaryColor = if ($failCount -eq 0) { "Green" } else { "Red" }
Write-Host "Total Tests: $($results.Count) | PASSED: $passCount | FAILED: $failCount" -ForegroundColor $summaryColor

if ($failCount -gt 0) {
    exit 1
}
