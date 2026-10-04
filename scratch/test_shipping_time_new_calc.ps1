# Test script for the new Shipping Time calculation
$baseUrl = "http://localhost:5124"
$ErrorActionPreference = "Stop"

Write-Host "=================================================="
Write-Host "TESTING NEW SHIPPING TIME CALCULATION FORMULA"
Write-Host "Formula: External Loaded Time - Internal Loaded Time"
Write-Host "=================================================="

# 1. Login
$loginBody = @{ identifier = "transport@facestone.com"; password = "Password123!" } | ConvertTo-Json
$loginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginRes.data.token
$headers = @{ "Authorization" = "Bearer $token" }
Write-Host "[OK] Logged in successfully." -ForegroundColor Green

$pdfPath = "F:\FaceStoneWebsite\scratch\test_files\sample_doc.pdf"
$jpgPath = "F:\FaceStoneWebsite\scratch\test_files\sample_photo.jpg"

function Submit-MultipartForm {
    param(
        [hashtable]$Fields,
        [string]$WbFile,
        [string]$DsFile,
        [string]$BolFile
    )

    $boundary = [System.Guid]::NewGuid().ToString()
    $LF = "`r`n"
    $bodyBytes = [System.IO.MemoryStream]::new()

    foreach ($key in $Fields.Keys) {
        $val = $Fields[$key]
        if ($val -ne $null) {
            $header = "--$boundary$LF" +
                      "Content-Disposition: form-data; name=`"$key`"$LF$LF" +
                      "$val$LF"
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($header)
            $bodyBytes.Write($bytes, 0, $bytes.Length)
        }
    }

    $fileMap = @{
        "weighbridgeAttachment" = $WbFile
        "dailySupplyAttachment" = $DsFile
        "billOfLadingAttachment" = $BolFile
    }

    foreach ($fileField in $fileMap.Keys) {
        $filePath = $fileMap[$fileField]
        if (-not [string]::IsNullOrEmpty($filePath) -and (Test-Path $filePath)) {
            $fileName = [System.IO.Path]::GetFileName($filePath)
            $mime = if ($fileName.EndsWith(".pdf")) { "application/pdf" } else { "image/jpeg" }
            $fileContent = [System.IO.File]::ReadAllBytes($filePath)
            $fileHeader = "--$boundary$LF" +
                          "Content-Disposition: form-data; name=`"$fileField`"; filename=`"$fileName`"$LF" +
                          "Content-Type: $mime$LF$LF"
            $hBytes = [System.Text.Encoding]::UTF8.GetBytes($fileHeader)
            $bodyBytes.Write($hBytes, 0, $hBytes.Length)
            $bodyBytes.Write($fileContent, 0, $fileContent.Length)
            $trailerBytes = [System.Text.Encoding]::UTF8.GetBytes($LF)
            $bodyBytes.Write($trailerBytes, 0, $trailerBytes.Length)
        }
    }

    $endBytes = [System.Text.Encoding]::UTF8.GetBytes("--$boundary--$LF")
    $bodyBytes.Write($endBytes, 0, $endBytes.Length)
    $payloadBytes = $bodyBytes.ToArray()

    $req = [System.Net.HttpWebRequest]::Create("$baseUrl/api/transportation")
    $req.Method = "POST"
    $req.ContentType = "multipart/form-data; boundary=$boundary"
    $req.Headers["Authorization"] = "Bearer $token"
    $req.ContentLength = $payloadBytes.Length

    try {
        $stream = $req.GetRequestStream()
        $stream.Write($payloadBytes, 0, $payloadBytes.Length)
        $stream.Close()

        $resp = $req.GetResponse()
        $reader = [System.IO.StreamReader]::new($resp.GetResponseStream())
        $content = $reader.ReadToEnd()
        $reader.Close()
        return @{ StatusCode = [int]$resp.StatusCode; Content = $content; Success = $true }
    } catch [System.Net.WebException] {
        $errResp = $_.Exception.Response
        if ($errResp -ne $null) {
            $reader = [System.IO.StreamReader]::new($errResp.GetResponseStream())
            $content = $reader.ReadToEnd()
            $reader.Close()
            return @{ StatusCode = [int]$errResp.StatusCode; Content = $content; Success = $false }
        }
        throw $_
    }
}

# -----------------------------------------------------------------
# TEST 1: Internal Loaded = 08:30, External Loaded = 13:45
# Difference = 5 hours 15 mins = 315 minutes (05:15)
# -----------------------------------------------------------------
Write-Host "`n--- TEST 1: Internal Loaded 08:30, External Loaded 13:45 ---" -ForegroundColor Yellow
$fields1 = @{
    "poNumber" = "PO-400"
    "transactionDate" = "2026-10-04T10:00:00Z"
    "carNumber" = "ABC 101"
    "driverName" = "Test Driver 1"
    "materialType" = "powder"
    "internalWeighbridgeCode" = "INT-TEST-001"
    "externalWeighbridgeCode" = "EXT-TEST-001"
    "weightWhenExitingFactory" = "10.000"
    "weightBeforeLoading" = "2.000"
    "externalLoadedWeight" = "10.000"
    "externalEmptyWeight" = "2.000"
    "internalLoadedWeightTime" = "2026-10-04T08:30:00Z"
    "externalLoadedWeightTime" = "2026-10-04T13:45:00Z"
    "externalEmptyWeightTime"  = "2026-10-04T15:00:00Z" # Deliberately different to ensure it is NOT used!
    "receivingStatus" = "safe"
    "custodyAmount" = "100"
}

$res1 = Submit-MultipartForm -Fields $fields1 -WbFile $pdfPath -DsFile $pdfPath -BolFile $jpgPath
if (-not $res1.Success) {
    Write-Host "[FAIL] Test 1 request failed: $($res1.Content)" -ForegroundColor Red
    exit 1
}
$json1 = $res1.Content | ConvertFrom-Json
$data1 = $json1.data
$duration1 = $data1.shippingDurationMinutes
$hours1 = [int][Math]::Floor($duration1 / 60)
$mins1 = [int]($duration1 % 60)
$formatted1 = "{0:D2}:{1:D2}" -f $hours1, $mins1

if ($duration1 -eq 315 -and $formatted1 -eq "05:15") {
    Write-Host "[PASS] Test 1: Duration = $duration1 mins ($formatted1) - Exactly matches 05:15!" -ForegroundColor Green
} else {
    Write-Host "[FAIL] Test 1: Expected 315 mins (05:15), got $duration1 mins ($formatted1)" -ForegroundColor Red
    exit 1
}

# -----------------------------------------------------------------
# TEST 2: Internal Loaded = 10:00, External Loaded = 10:00
# Difference = 0 mins (00:00)
# -----------------------------------------------------------------
Write-Host "`n--- TEST 2: Internal Loaded 10:00, External Loaded 10:00 ---" -ForegroundColor Yellow
$fields2 = @{
    "poNumber" = "PO-400"
    "transactionDate" = "2026-10-04T10:00:00Z"
    "carNumber" = "ABC 102"
    "driverName" = "Test Driver 2"
    "materialType" = "powder"
    "internalWeighbridgeCode" = "INT-TEST-002"
    "externalWeighbridgeCode" = "EXT-TEST-002"
    "weightWhenExitingFactory" = "8.000"
    "weightBeforeLoading" = "2.000"
    "externalLoadedWeight" = "8.000"
    "externalEmptyWeight" = "2.000"
    "internalLoadedWeightTime" = "2026-10-04T10:00:00Z"
    "externalLoadedWeightTime" = "2026-10-04T10:00:00Z"
    "externalEmptyWeightTime"  = "2026-10-04T11:00:00Z" # Deliberately different!
    "receivingStatus" = "safe"
    "custodyAmount" = "50"
}

$res2 = Submit-MultipartForm -Fields $fields2 -WbFile $pdfPath -DsFile $pdfPath -BolFile $jpgPath
if (-not $res2.Success) {
    Write-Host "[FAIL] Test 2 request failed: $($res2.Content)" -ForegroundColor Red
    exit 1
}
$json2 = $res2.Content | ConvertFrom-Json
$data2 = $json2.data
$duration2 = $data2.shippingDurationMinutes
$hours2 = [int][Math]::Floor($duration2 / 60)
$mins2 = [int]($duration2 % 60)
$formatted2 = "{0:D2}:{1:D2}" -f $hours2, $mins2

if ($duration2 -eq 0 -and $formatted2 -eq "00:00") {
    Write-Host "[PASS] Test 2: Duration = $duration2 mins ($formatted2) - Exactly matches 00:00!" -ForegroundColor Green
} else {
    Write-Host "[FAIL] Test 2: Expected 0 mins (00:00), got $duration2 mins ($formatted2)" -ForegroundColor Red
    exit 1
}

# -----------------------------------------------------------------
# TEST 3: Internal Loaded = 14:00, External Loaded = 12:00
# External Loaded < Internal Loaded -> Validation Error (HTTP 400)
# -----------------------------------------------------------------
Write-Host "`n--- TEST 3: Internal Loaded 14:00, External Loaded 12:00 (Negative Shipping Time) ---" -ForegroundColor Yellow
$fields3 = @{
    "poNumber" = "PO-400"
    "transactionDate" = "2026-10-04T10:00:00Z"
    "carNumber" = "ABC 103"
    "driverName" = "Test Driver 3"
    "materialType" = "powder"
    "internalWeighbridgeCode" = "INT-TEST-003"
    "externalWeighbridgeCode" = "EXT-TEST-003"
    "weightWhenExitingFactory" = "8.000"
    "weightBeforeLoading" = "2.000"
    "externalLoadedWeight" = "8.000"
    "externalEmptyWeight" = "2.000"
    "internalLoadedWeightTime" = "2026-10-04T14:00:00Z"
    "externalLoadedWeightTime" = "2026-10-04T12:00:00Z"
    "externalEmptyWeightTime"  = "2026-10-04T16:00:00Z"
    "receivingStatus" = "safe"
    "custodyAmount" = "50"
}

$res3 = Submit-MultipartForm -Fields $fields3 -WbFile $pdfPath -DsFile $pdfPath -BolFile $jpgPath
if ($res3.StatusCode -eq 400 -and $res3.Success -eq $false) {
    Write-Host "[PASS] Test 3: Correctly rejected with HTTP 400. Response: $($res3.Content)" -ForegroundColor Green
} else {
    Write-Host "[FAIL] Test 3: Expected HTTP 400 rejection for negative shipping time, got $($res3.StatusCode)" -ForegroundColor Red
    exit 1
}

Write-Host "`n=================================================="
Write-Host "ALL BACKEND TESTS PASSED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "=================================================="
