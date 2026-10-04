# Comprehensive Verification Test Suite for Transportation Department Refinement
$ErrorActionPreference = "Stop"
$baseUrl = "http://localhost:5124"

# 1. Start application if not running
$tcpConn = Get-NetTCPConnection -LocalPort 5124 -ErrorAction SilentlyContinue
$startedProcess = $null

if (-not $tcpConn) {
    Write-Host "[TEST] Starting FaceStone.Web on port 5124..." -ForegroundColor Cyan
    $startedProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "FaceStone.Web.csproj", "--no-build" -PassThru -WindowStyle Hidden
    
    $healthy = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 1
        try {
            $h = Invoke-RestMethod -Uri "$baseUrl/health" -TimeoutSec 2 -ErrorAction Stop
            if ($h.status -eq "Healthy") {
                $healthy = $true
                Write-Host "[TEST] Server is healthy and responsive." -ForegroundColor Green
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
    Write-Host "[TEST] Server is already running on port 5124." -ForegroundColor Green
}

# Helper for HTTP requests in Windows PowerShell 5.1
function Invoke-SafeRequest {
    param(
        [string]$Uri,
        [string]$Method = "Get",
        [hashtable]$Headers = @{},
        [string]$ContentType = $null,
        [byte[]]$Body = $null
    )
    try {
        $params = @{
            Uri = $Uri
            Method = $Method
            Headers = $Headers
            UseBasicParsing = $true
        }
        if ($ContentType) { $params["ContentType"] = $ContentType }
        if ($Body) { $params["Body"] = $Body }
        $resp = Invoke-WebRequest @params
        return @{
            StatusCode = [int]$resp.StatusCode
            Content = $resp.Content
        }
    } catch [System.Net.WebException] {
        $webEx = $_.Exception
        if ($webEx.Response) {
            $stream = $webEx.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $content = $reader.ReadToEnd()
            $code = [int]$webEx.Response.StatusCode
            return @{
                StatusCode = $code
                Content = $content
            }
        }
        throw $_
    }
}

try {
    # Helper to authenticate
    function Get-Token($identifier, $password) {
        $body = @{ identifier = $identifier; password = $password } | ConvertTo-Json
        $res = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $body -ContentType "application/json"
        return $res.data.token
    }

    $token = Get-Token "kareem.ceo@facestone.com" "Password123!"
    $headers = @{ Authorization = "Bearer $token" }
    Write-Host "[AUTH] Authenticated as CEO (kareem.ceo@facestone.com)." -ForegroundColor Green

    # Create dummy valid PDF and JPG files with correct magic bytes
    $scratchDir = "F:\FaceStoneWebsite\scratch\test_files"
    if (-not (Test-Path $scratchDir)) { New-Item -ItemType Directory -Path $scratchDir -Force | Out-Null }

    $validPdfPath = Join-Path $scratchDir "sample_doc.pdf"
    $pdfBytes = [System.Text.Encoding]::ASCII.GetBytes("%PDF-1.4 test document content for Transportation Department")
    [System.IO.File]::WriteAllBytes($validPdfPath, $pdfBytes)

    $validJpgPath = Join-Path $scratchDir "sample_photo.jpg"
    $jpgBytes = [byte[]](0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01)
    [System.IO.File]::WriteAllBytes($validJpgPath, $jpgBytes)

    $invalidTxtPath = Join-Path $scratchDir "bad_file.txt"
    [System.IO.File]::WriteAllText($invalidTxtPath, "This is not allowed")

    # Helper function to submit multipart form data
    function Submit-TransportationForm {
        param(
            [hashtable]$Fields,
            [string]$WbFile,
            [string]$DsFile,
            [string]$BolFile
        )

        $boundary = [System.Guid]::NewGuid().ToString()
        $LF = "`r`n"
        $memoryStream = New-Object System.IO.MemoryStream

        # Default required fields if missing
        if (-not $Fields.ContainsKey("transactionDate") -or $null -eq $Fields["transactionDate"]) {
            $Fields["transactionDate"] = "2026-10-03T10:00:00Z"
        }
        if (-not $Fields.ContainsKey("internalWeighbridgeCode") -or $null -eq $Fields["internalWeighbridgeCode"]) {
            $Fields["internalWeighbridgeCode"] = "WB-001"
        }
        if (-not $Fields.ContainsKey("externalWeighbridgeCode") -or $null -eq $Fields["externalWeighbridgeCode"]) {
            $Fields["externalWeighbridgeCode"] = "EXT-555"
        }
        if (-not $Fields.ContainsKey("internalLoadedWeightTime") -or $null -eq $Fields["internalLoadedWeightTime"]) {
            $Fields["internalLoadedWeightTime"] = "2026-10-03T08:30:00Z"
        }
        if (-not $Fields.ContainsKey("externalLoadedWeightTime") -or $null -eq $Fields["externalLoadedWeightTime"]) {
            $Fields["externalLoadedWeightTime"] = "2026-10-03T12:45:00Z"
        }
        if (-not $Fields.ContainsKey("externalEmptyWeightTime") -or $null -eq $Fields["externalEmptyWeightTime"]) {
            $Fields["externalEmptyWeightTime"] = "2026-10-03T11:30:00Z"
        }
        if (-not $Fields.ContainsKey("custodyAmount") -or $null -eq $Fields["custodyAmount"]) {
            $Fields["custodyAmount"] = "1250.50"
        }

        # Write text fields
        foreach ($key in $Fields.Keys) {
            $val = $Fields[$key]
            if ($val -ne $null) {
                $fieldHeader = "--$boundary$LF" +
                    "Content-Disposition: form-data; name=`"$key`"$LF$LF" +
                    "$val$LF"
                $bytes = [System.Text.Encoding]::UTF8.GetBytes($fieldHeader)
                $memoryStream.Write($bytes, 0, $bytes.Length)
            }
        }

        # Helper to write file
        function Write-FilePart($fieldName, $filePath, $contentType) {
            if ($filePath -and (Test-Path $filePath)) {
                $fileName = [System.IO.Path]::GetFileName($filePath)
                $fileHeader = "--$boundary$LF" +
                    "Content-Disposition: form-data; name=`"$fieldName`"; filename=`"$fileName`"$LF" +
                    "Content-Type: $contentType$LF$LF"
                $hBytes = [System.Text.Encoding]::UTF8.GetBytes($fileHeader)
                $memoryStream.Write($hBytes, 0, $hBytes.Length)

                $fileData = [System.IO.File]::ReadAllBytes($filePath)
                $memoryStream.Write($fileData, 0, $fileData.Length)

                $trailing = [System.Text.Encoding]::UTF8.GetBytes($LF)
                $memoryStream.Write($trailing, 0, $trailing.Length)
            }
        }

        if ($WbFile) { Write-FilePart "weighbridgeAttachment" $WbFile "application/pdf" }
        if ($DsFile) { Write-FilePart "dailySupplyAttachment" $DsFile "application/pdf" }
        if ($BolFile) { Write-FilePart "billOfLadingAttachment" $BolFile "image/jpeg" }

        $footer = [System.Text.Encoding]::UTF8.GetBytes("--$boundary--$LF")
        $memoryStream.Write($footer, 0, $footer.Length)

        $bodyBytes = $memoryStream.ToArray()
        $memoryStream.Dispose()

        $reqHeaders = @{
            Authorization = "Bearer $token"
        }

        return Invoke-SafeRequest -Uri "$baseUrl/api/transportation" -Method "Post" -Headers $reqHeaders -ContentType "multipart/form-data; boundary=$boundary" -Body $bodyBytes
    }

    Write-Host ""
    Write-Host "=== SCENARIO 1: PO LOOKUP VALIDATION ===" -ForegroundColor Yellow
    # 1.1 Valid PO lookup
    $poRes = Invoke-RestMethod -Uri "$baseUrl/api/transportation/po/PO-100" -Method Get -Headers $headers
    Write-Host "PO-100 Lookup: Company=$($poRes.data.companyName), Destination=$($poRes.data.destination), NextTx=$($poRes.data.nextTransactionNumber)" -ForegroundColor Green
    if (-not $poRes.data.destination -or -not $poRes.data.companyName) { throw "PO company or destination missing" }

    # 1.2 Invalid PO lookup
    $badPoRes = Invoke-SafeRequest -Uri "$baseUrl/api/transportation/po/PO-NONEXISTENT-999" -Method "Get" -Headers $headers
    if ($badPoRes.StatusCode -ne 404) { throw "Expected 404 for invalid PO, got $($badPoRes.StatusCode)" }
    Write-Host "Invalid PO correctly rejected with 404." -ForegroundColor Green

    Write-Host ""
    Write-Host "=== SCENARIO 2: ATTACHMENT VALIDATIONS (ALL 3 SECTIONS MANDATORY) ===" -ForegroundColor Yellow
    $baseFields = @{
        "poNumber" = "PO-300"
        "transactionDate" = "2026-10-03T10:00:00Z"
        "carNumber" = "MSR 777"
        "driverName" = "Ahmed Driver"
        "materialType" = "powder"
        "weightWhenExitingFactory" = "10000"
        "weightBeforeLoading" = "2000"
        "externalLoadedWeight" = "9800"
        "externalEmptyWeight" = "2000"
        "internalWeighbridgeCode" = "WB-001"
        "externalWeighbridgeCode" = "EXT-555"
        "internalLoadedWeightTime" = "2026-10-03T08:00:00Z"
        "externalEmptyWeightTime" = "2026-10-03T11:30:00Z"
        "externalLoadedWeightTime" = "2026-10-03T12:45:00Z"
        "receivingStatus" = "safe"
        "custodyAmount" = "1250.50"
    }

    # 2.1 Missing Section 1 Weighbridge attachment
    $resNoWb = Submit-TransportationForm -Fields $baseFields -WbFile $null -DsFile $validPdfPath -BolFile $validJpgPath
    Write-Host "Missing Weighbridge attachment status: $($resNoWb.StatusCode)" -ForegroundColor Cyan
    $jsonNoWb = $resNoWb.Content | ConvertFrom-Json
    if ($resNoWb.StatusCode -ne 400 -or -not ($jsonNoWb.message -like "*Attachment is required*")) { throw "Should reject missing weighbridge attachment: $($resNoWb.Content)" }
    Write-Host "Missing Section 1 attachment blocked: $($jsonNoWb.message)" -ForegroundColor Green

    # 2.2 Missing Section 2 Daily Supply attachment
    $resNoDs = Submit-TransportationForm -Fields $baseFields -WbFile $validPdfPath -DsFile $null -BolFile $validJpgPath
    $jsonNoDs = $resNoDs.Content | ConvertFrom-Json
    if ($resNoDs.StatusCode -ne 400 -or -not ($jsonNoDs.message -like "*Attachment is required*")) { throw "Should reject missing daily supply attachment" }
    Write-Host "Missing Section 2 attachment blocked: $($jsonNoDs.message)" -ForegroundColor Green

    # 2.3 Missing Section 3 Bill of Lading attachment
    $resNoBol = Submit-TransportationForm -Fields $baseFields -WbFile $validPdfPath -DsFile $validPdfPath -BolFile $null
    $jsonNoBol = $resNoBol.Content | ConvertFrom-Json
    if ($resNoBol.StatusCode -ne 400 -or -not ($jsonNoBol.message -like "*Attachment is required*")) { throw "Should reject missing bill of lading attachment" }
    Write-Host "Missing Section 3 attachment blocked: $($jsonNoBol.message)" -ForegroundColor Green

    # 2.4 Unsupported file format (e.g. .txt)
    $resBadExt = Submit-TransportationForm -Fields $baseFields -WbFile $invalidTxtPath -DsFile $validPdfPath -BolFile $validJpgPath
    $jsonBadExt = $resBadExt.Content | ConvertFrom-Json
    if ($resBadExt.StatusCode -ne 400) { throw "Should reject invalid file type" }
    Write-Host "Invalid attachment format blocked: $($jsonBadExt.message)" -ForegroundColor Green

    Write-Host ""
    Write-Host "=== SCENARIO 3: SEQUENTIAL TRANSACTION NUMBERING PER PO ===" -ForegroundColor Yellow
    # 3.1 PO-300: Transaction 1
    $po300Initial = (Invoke-RestMethod -Uri "$baseUrl/api/transportation/po/PO-300" -Method Get -Headers $headers).data
    Write-Host "PO-300 Remaining before Tx 1: $($po300Initial.remainingQuantity)" -ForegroundColor Cyan

    $tx1Fields = @{
        "poNumber" = "PO-300"
        "carNumber" = "MSR 777"
        "driverName" = "Ahmed Driver"
        "materialType" = "powder"
        "weightWhenExitingFactory" = "10000"
        "weightBeforeLoading" = "2000"
        "externalLoadedWeight" = "9800"
        "externalEmptyWeight" = "2000"
        "internalWeighbridgeCode" = "WB-001"
        "externalWeighbridgeCode" = "EXT-555"
        "internalLoadedWeightTime" = "2026-10-03T08:00:00Z"
        "externalEmptyWeightTime" = "2026-10-03T11:30:00Z"
        "externalLoadedWeightTime" = "2026-10-03T12:45:00Z"
        "receivingStatus" = "safe"
        "ikramiya" = "50.00"
        "custodyAmount" = "1250.50"
    }

    $resTx1 = Submit-TransportationForm -Fields $tx1Fields -WbFile $validPdfPath -DsFile $validPdfPath -BolFile $validJpgPath
    if ($resTx1.StatusCode -ne 200) { throw "Failed to submit PO-300 Tx 1: $($resTx1.Content)" }
    $dataTx1 = ($resTx1.Content | ConvertFrom-Json).data
    Write-Host "PO-300 Transaction 1 created: TxNumber=$($dataTx1.transactionNumber), NetWeight=$($dataTx1.netWeight), Waste=$($dataTx1.wasteQuantity), PO Remaining After=$($dataTx1.poRemainingQuantityAfter)" -ForegroundColor Green
    if ($dataTx1.transactionNumber -ne 1) { throw "Expected TxNumber 1 for new PO-300, got $($dataTx1.transactionNumber)" }
    if ($dataTx1.netWeight -ne 7800) { throw "Expected NetWeight 7800, got $($dataTx1.netWeight)" }
    if ($dataTx1.wasteQuantity -ne 200) { throw "Expected Waste 200, got $($dataTx1.wasteQuantity)" }

    # Verify Manual Weighbridge Reference Codes (Requirements 1A, 1B)
    if ($dataTx1.internalWeighbridgeCode -ne "WB-001") { throw "Expected internalWeighbridgeCode 'WB-001', got '$($dataTx1.internalWeighbridgeCode)'" }
    if ($dataTx1.externalWeighbridgeCode -ne "EXT-555") { throw "Expected externalWeighbridgeCode 'EXT-555', got '$($dataTx1.externalWeighbridgeCode)'" }
    Write-Host "Manual weighbridge codes verified: Internal='WB-001', External='EXT-555'." -ForegroundColor Green

    # Verify Derived Departure and Arrival Dates (Requirements 2, 3, 4)
    if (-not $dataTx1.departureTime -or -not ($dataTx1.departureTime -like "*08:00*")) {
        throw "Expected departureTime derived from internal loaded time (08:00). Got '$($dataTx1.departureTime)'"
    }
    if (-not $dataTx1.arrivalTime -or -not ($dataTx1.arrivalTime -like "*12:45*")) {
        throw "Expected arrivalTime derived from external loaded time (12:45). Got '$($dataTx1.arrivalTime)'"
    }
    Write-Host "Derived dates verified: DepartureTime=$($dataTx1.departureTime), ArrivalTime=$($dataTx1.arrivalTime)." -ForegroundColor Green

    # Verify Custody (Requirements 6, 7)
    if ($dataTx1.custodyAmount -ne 1250.50) { throw "Expected CustodyAmount 1250.50, got $($dataTx1.custodyAmount)" }
    Write-Host "Custody verified: $($dataTx1.custodyAmount) EGP." -ForegroundColor Green

    # Verify PO Remaining deduction: 50000 - 7800 = 42200
    if ($dataTx1.poRemainingQuantityAfter -ne ($po300Initial.remainingQuantity - 7800)) {
        throw "Expected PO remaining deduction of 7800. Got $($dataTx1.poRemainingQuantityAfter)"
    }
    Write-Host "PO deduction verified: $($po300Initial.remainingQuantity) - 7800 = $($dataTx1.poRemainingQuantityAfter)" -ForegroundColor Green

    # 3.2 PO-300: Transaction 2 (Equal weights: 8000 and 8000 -> Waste=0, Net=8000)
    $tx2Fields = @{
        "poNumber" = "PO-300"
        "carNumber" = "SFN 123"
        "driverName" = "Ahmed Driver"
        "materialType" = "slurry"
        "weightWhenExitingFactory" = "10000"
        "weightBeforeLoading" = "2000"
        "externalLoadedWeight" = "10000"
        "externalEmptyWeight" = "2000"
        "internalWeighbridgeCode" = "WB-002"
        "externalWeighbridgeCode" = "EXT-556"
        "internalLoadedWeightTime" = "2026-10-03T09:00:00Z"
        "externalEmptyWeightTime" = "2026-10-03T11:00:00Z"
        "externalLoadedWeightTime" = "2026-10-03T12:00:00Z"
        "receivingStatus" = "intact"
        "custodyAmount" = "500.00"
    }
    $resTx2 = Submit-TransportationForm -Fields $tx2Fields -WbFile $validPdfPath -DsFile $validPdfPath -BolFile $validJpgPath
    if ($resTx2.StatusCode -ne 200) { throw "Failed to submit PO-300 Tx 2: $($resTx2.Content)" }
    $dataTx2 = ($resTx2.Content | ConvertFrom-Json).data
    Write-Host "PO-300 Transaction 2 created: TxNumber=$($dataTx2.transactionNumber), NetWeight=$($dataTx2.netWeight), Waste=$($dataTx2.wasteQuantity)" -ForegroundColor Green
    if ($dataTx2.transactionNumber -ne 2) { throw "Expected TxNumber 2 for PO-300, got $($dataTx2.transactionNumber)" }
    if ($dataTx2.netWeight -ne 8000 -or $dataTx2.wasteQuantity -ne 0) { throw "Expected NetWeight 8000 and Waste 0" }

    # 3.3 PO-400: Transaction 1 (MUST start at 1 for new PO!)
    $txPo400_1 = @{
        "poNumber" = "PO-400"
        "carNumber" = "GMA 555"
        "driverName" = "Ahmed Driver"
        "materialType" = "powder"
        "weightWhenExitingFactory" = "7000"
        "weightBeforeLoading" = "2000"
        "externalLoadedWeight" = "7000"
        "externalEmptyWeight" = "2000"
        "internalWeighbridgeCode" = "WB-P2-01"
        "externalWeighbridgeCode" = "EXT-P2-01"
        "internalLoadedWeightTime" = "2026-10-03T07:00:00Z"
        "externalEmptyWeightTime" = "2026-10-03T09:30:00Z"
        "externalLoadedWeightTime" = "2026-10-03T10:00:00Z"
        "receivingStatus" = "safe, intact"
        "custodyAmount" = "0.00"
    }
    $resPo400_1 = Submit-TransportationForm -Fields $txPo400_1 -WbFile $validPdfPath -DsFile $validPdfPath -BolFile $validJpgPath
    if ($resPo400_1.StatusCode -ne 200) { throw "Failed to submit PO-400 Tx 1: $($resPo400_1.Content)" }
    $dataPo400_1 = ($resPo400_1.Content | ConvertFrom-Json).data
    Write-Host "PO-400 Transaction 1 created: TxNumber=$($dataPo400_1.transactionNumber)" -ForegroundColor Green
    if ($dataPo400_1.transactionNumber -ne 1) { throw "Expected TxNumber 1 for new PO-400, got $($dataPo400_1.transactionNumber)" }

    # 3.4 PO-400: Transaction 2
    $txPo400_2 = @{
        "poNumber" = "PO-400"
        "carNumber" = "GMA 555"
        "driverName" = "Ahmed Driver"
        "materialType" = "powder"
        "weightWhenExitingFactory" = "6000"
        "weightBeforeLoading" = "2000"
        "externalLoadedWeight" = "6000"
        "externalEmptyWeight" = "2000"
        "internalWeighbridgeCode" = "WB-P2-02"
        "externalWeighbridgeCode" = "EXT-P2-02"
        "internalLoadedWeightTime" = "2026-10-03T07:00:00Z"
        "externalEmptyWeightTime" = "2026-10-03T09:30:00Z"
        "externalLoadedWeightTime" = "2026-10-03T10:00:00Z"
        "receivingStatus" = "safe"
        "custodyAmount" = "250.75"
    }
    $resPo400_2 = Submit-TransportationForm -Fields $txPo400_2 -WbFile $validPdfPath -DsFile $validPdfPath -BolFile $validJpgPath
    if ($resPo400_2.StatusCode -ne 200) { throw "Failed to submit PO-400 Tx 2: $($resPo400_2.Content)" }
    $dataPo400_2 = ($resPo400_2.Content | ConvertFrom-Json).data
    Write-Host "PO-400 Transaction 2 created: TxNumber=$($dataPo400_2.transactionNumber)" -ForegroundColor Green
    if ($dataPo400_2.transactionNumber -ne 2) { throw "Expected TxNumber 2 for PO-400, got $($dataPo400_2.transactionNumber)" }

    # 3.5 PO-300: Transaction 3
    $tx300_3 = @{
        "poNumber" = "PO-300"
        "carNumber" = "SFN 999"
        "driverName" = "Ahmed Driver"
        "materialType" = "powder"
        "weightWhenExitingFactory" = "5000"
        "weightBeforeLoading" = "2000"
        "externalLoadedWeight" = "5000"
        "externalEmptyWeight" = "2000"
        "internalWeighbridgeCode" = "WB-003"
        "externalWeighbridgeCode" = "EXT-557"
        "internalLoadedWeightTime" = "2026-10-03T07:00:00Z"
        "externalEmptyWeightTime" = "2026-10-03T09:30:00Z"
        "externalLoadedWeightTime" = "2026-10-03T10:00:00Z"
        "receivingStatus" = "safe"
        "custodyAmount" = "100.00"
    }
    $res300_3 = Submit-TransportationForm -Fields $tx300_3 -WbFile $validPdfPath -DsFile $validPdfPath -BolFile $validJpgPath
    if ($res300_3.StatusCode -ne 200) { throw "Failed to submit PO-300 Tx 3: $($res300_3.Content)" }
    $data300_3 = ($res300_3.Content | ConvertFrom-Json).data
    Write-Host "PO-300 Transaction 3 created: TxNumber=$($data300_3.transactionNumber)" -ForegroundColor Green
    if ($data300_3.transactionNumber -ne 3) { throw "Expected TxNumber 3 for PO-300, got $($data300_3.transactionNumber)" }

    Write-Host "Composite uniqueness and per-PO sequential numbering verified perfectly!" -ForegroundColor Green

    Write-Host ""
    Write-Host "=== SCENARIO 4: SHIPPING TIME AND DURATION CHECKS ===" -ForegroundColor Yellow
    # 4.1 Negative shipping time check
    $negTimeFields = @{
        "poNumber" = "PO-400"
        "carNumber" = "MSR 777"
        "driverName" = "Ahmed Driver"
        "materialType" = "powder"
        "weightWhenExitingFactory" = "5000"
        "weightBeforeLoading" = "2000"
        "externalLoadedWeight" = "5000"
        "externalEmptyWeight" = "2000"
        "internalLoadedWeightTime" = "2026-10-03T14:00:00Z"
        "externalEmptyWeightTime" = "2026-10-03T10:00:00Z" # Before start!
        "receivingStatus" = "safe"
    }
    $resNegTime = Submit-TransportationForm -Fields $negTimeFields -WbFile $validPdfPath -DsFile $validPdfPath -BolFile $validJpgPath
    if ($resNegTime.StatusCode -ne 400) { throw "Should reject negative shipping time" }
    Write-Host "Negative shipping time correctly rejected: $(($resNegTime.Content | ConvertFrom-Json).message)" -ForegroundColor Green

    # 4.2 Valid shipping time check: 08:00 to 11:30 = 210 minutes (3h 30m)
    if ($dataTx1.shippingDurationMinutes -ne 210) {
        throw "Expected 210 minutes for 08:00 to 11:30, got $($dataTx1.shippingDurationMinutes)"
    }
    Write-Host "Shipping time duration calculated accurately: $($dataTx1.shippingDurationMinutes) mins (03:30:00)." -ForegroundColor Green

    Write-Host ""
    Write-Host "=== SCENARIO 5: DELIVERY STATUS CHECKLIST ===" -ForegroundColor Yellow
    # 5.1 Invalid delivery status
    $badStatusFields = @{
        "poNumber" = "PO-400"
        "carNumber" = "MSR 777"
        "driverName" = "Ahmed Driver"
        "materialType" = "powder"
        "weightWhenExitingFactory" = "5000"
        "weightBeforeLoading" = "2000"
        "externalLoadedWeight" = "5000"
        "externalEmptyWeight" = "2000"
        "receivingStatus" = "damaged_goods" # Invalid
    }
    $resBadStatus = Submit-TransportationForm -Fields $badStatusFields -WbFile $validPdfPath -DsFile $validPdfPath -BolFile $validJpgPath
    if ($resBadStatus.StatusCode -ne 400) { throw "Should reject invalid delivery status" }
    Write-Host "Invalid delivery status rejected as expected." -ForegroundColor Green

    Write-Host ""
    Write-Host "=== SCENARIO 6: PO QUANTITY LIMIT AND EXCEEDING REMAINING ===" -ForegroundColor Yellow
    # Check remaining for PO-400
    $po400Cur = (Invoke-RestMethod -Uri "$baseUrl/api/transportation/po/PO-400" -Method Get -Headers $headers).data
    Write-Host "PO-400 Current remaining quantity: $($po400Cur.remainingQuantity)" -ForegroundColor Cyan

    $exceedFields = @{
        "poNumber" = "PO-400"
        "carNumber" = "MSR 777"
        "driverName" = "Ahmed Driver"
        "materialType" = "powder"
        "weightWhenExitingFactory" = "100000"
        "weightBeforeLoading" = "2000" # Net = 98,000 > Remaining
        "externalLoadedWeight" = "100000"
        "externalEmptyWeight" = "2000"
        "receivingStatus" = "safe"
    }
    $resExceed = Submit-TransportationForm -Fields $exceedFields -WbFile $validPdfPath -DsFile $validPdfPath -BolFile $validJpgPath
    $jsonExceed = $resExceed.Content | ConvertFrom-Json
    if ($resExceed.StatusCode -ne 400 -or -not ($jsonExceed.message -like "*exceeds the remaining quantity*")) {
        throw "Should reject when net weight exceeds PO remaining quantity"
    }
    Write-Host "Exceeding PO quantity blocked: $($jsonExceed.message)" -ForegroundColor Green

    Write-Host ""
    Write-Host "=== SCENARIO 7: MANUAL WEIGHBRIDGE REFERENCES AND CUSTODY VALIDATION ===" -ForegroundColor Yellow
    # 7.1 Missing Internal Weighbridge Code
    $noIntCodeFields = @{
        "poNumber" = "PO-400"
        "carNumber" = "MSR 777"
        "driverName" = "Ahmed Driver"
        "materialType" = "powder"
        "weightWhenExitingFactory" = "5000"
        "weightBeforeLoading" = "2000"
        "externalLoadedWeight" = "5000"
        "externalEmptyWeight" = "2000"
        "internalWeighbridgeCode" = "" # Missing
        "externalWeighbridgeCode" = "EXT-123"
        "receivingStatus" = "safe"
        "custodyAmount" = "100.00"
    }
    $resNoIntCode = Submit-TransportationForm -Fields $noIntCodeFields -WbFile $validPdfPath -DsFile $validPdfPath -BolFile $validJpgPath
    if ($resNoIntCode.StatusCode -ne 400) { throw "Should reject missing internal weighbridge code" }
    Write-Host "Missing internal weighbridge code correctly rejected." -ForegroundColor Green

    # 7.2 Missing External Weighbridge Code
    $noExtCodeFields = @{
        "poNumber" = "PO-400"
        "carNumber" = "MSR 777"
        "driverName" = "Ahmed Driver"
        "materialType" = "powder"
        "weightWhenExitingFactory" = "5000"
        "weightBeforeLoading" = "2000"
        "externalLoadedWeight" = "5000"
        "externalEmptyWeight" = "2000"
        "internalWeighbridgeCode" = "WB-123"
        "externalWeighbridgeCode" = "" # Missing
        "receivingStatus" = "safe"
        "custodyAmount" = "100.00"
    }
    $resNoExtCode = Submit-TransportationForm -Fields $noExtCodeFields -WbFile $validPdfPath -DsFile $validPdfPath -BolFile $validJpgPath
    if ($resNoExtCode.StatusCode -ne 400) { throw "Should reject missing external weighbridge code" }
    Write-Host "Missing external weighbridge code correctly rejected." -ForegroundColor Green

    # 7.3 Negative Custody Amount
    $negCustodyFields = @{
        "poNumber" = "PO-400"
        "carNumber" = "MSR 777"
        "driverName" = "Ahmed Driver"
        "materialType" = "powder"
        "weightWhenExitingFactory" = "5000"
        "weightBeforeLoading" = "2000"
        "externalLoadedWeight" = "5000"
        "externalEmptyWeight" = "2000"
        "internalWeighbridgeCode" = "WB-123"
        "externalWeighbridgeCode" = "EXT-123"
        "receivingStatus" = "safe"
        "custodyAmount" = "-50.00" # Negative
    }
    $resNegCustody = Submit-TransportationForm -Fields $negCustodyFields -WbFile $validPdfPath -DsFile $validPdfPath -BolFile $validJpgPath
    if ($resNegCustody.StatusCode -ne 400) { throw "Should reject negative custody amount" }
    Write-Host "Negative custody amount correctly rejected." -ForegroundColor Green

    Write-Host ""
    Write-Host "=== SCENARIO 8: UNRELATED DEPARTMENTS INTEGRITY VERIFICATION ===" -ForegroundColor Yellow
    # 8.1 Finance Department
    $finVerify = Invoke-RestMethod -Uri "$baseUrl/api/department/finance" -Method Get -Headers $headers
    if ($finVerify.success -ne $true) { throw "Finance department affected!" }
    Write-Host "Finance department verified intact." -ForegroundColor Green

    # 8.2 Storage Department
    $storVerify = Invoke-RestMethod -Uri "$baseUrl/api/department/storage" -Method Get -Headers $headers
    if ($storVerify.success -ne $true) { throw "Storage department affected!" }
    Write-Host "Storage department verified intact." -ForegroundColor Green

    # 8.3 Management Department
    $mgmtVerify = Invoke-RestMethod -Uri "$baseUrl/api/department/management" -Method Get -Headers $headers
    if ($mgmtVerify.success -ne $true) { throw "Management department affected!" }
    Write-Host "Management department verified intact." -ForegroundColor Green

    Write-Host ""
    Write-Host "========================================================" -ForegroundColor Green
    Write-Host "ALL 8 TEST SUITES PASSED FLAWLESSLY WITH 100% SUCCESS!" -ForegroundColor Green
    Write-Host "========================================================" -ForegroundColor Green

} finally {
    if ($startedProcess) {
        Write-Host "[CLEANUP] Stopping started dotnet process $($startedProcess.Id)..." -ForegroundColor Cyan
        Stop-Process -Id $startedProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
