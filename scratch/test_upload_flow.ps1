$baseUrl = "http://localhost:5124"
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

Write-Host "=== STEP 1: AUTHENTICATING USERS ===" -ForegroundColor Cyan
$ceoLogin = @{ Identifier = "kareem_ceo"; Password = "Password123!" } | ConvertTo-Json
$ceoRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $ceoLogin -ContentType "application/json"
$ceoToken = $ceoRes.data.token
Write-Host "CEO Authenticated: $($ceoRes.data.user.fullName)" -ForegroundColor Green

$cmLogin = @{ Identifier = "control_monitoring"; Password = "Password123!" } | ConvertTo-Json
$cmRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $cmLogin -ContentType "application/json"
$cmToken = $cmRes.data.token
Write-Host "Control Monitoring Authenticated: $($cmRes.data.user.fullName)" -ForegroundColor Green

Write-Host "`n=== STEP 2: CREATING SAMPLE ATTACHMENT FILES ===" -ForegroundColor Cyan
$scratchDir = "f:\FaceStoneWebsite\scratch\test_att"
if (!(Test-Path $scratchDir)) { New-Item -ItemType Directory -Path $scratchDir | Out-Null }

# 1. Valid PDF file
$pdfPath = Join-Path $scratchDir "official_weighbridge.pdf"
$pdfContent = "%PDF-1.4`n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj`n2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj`n3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R/Resources<<>>>>endobj`nxref`n0 4`n0000000000 65535 f `n0000000010 00000 n `n0000000060 00000 n `n0000000117 00000 n `ntrailer<</Size 4/Root 1 0 R>>`nstartxref`n200`n%%EOF`n"
[System.IO.File]::WriteAllBytes($pdfPath, [System.Text.Encoding]::ASCII.GetBytes($pdfContent))
$origPdfHash = (Get-FileHash $pdfPath -Algorithm SHA256).Hash
Write-Host "Generated PDF: $pdfPath (SHA256: $origPdfHash)" -ForegroundColor Green

# 2. Valid JPG file
$jpgPath = Join-Path $scratchDir "truck_scale_photo.jpg"
$jpgBytes = [byte[]](
    0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
    0x01, 0x01, 0x00, 0x48, 0x00, 0x48, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43,
    0x00, 0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08, 0x07, 0x07, 0x07, 0x09,
    0x09, 0x08, 0x0A, 0x0C, 0x14, 0x0D, 0x0C, 0x0B, 0x0B, 0x0C, 0x19, 0x12,
    0x13, 0x0F, 0x14, 0x1D, 0x1A, 0x1F, 0x1E, 0x1D, 0x1A, 0x1C, 0x1C, 0x20,
    0x24, 0x2E, 0x27, 0x20, 0x22, 0x2C, 0x23, 0x1C, 0x1C, 0x28, 0x37, 0x29,
    0x2C, 0x30, 0x31, 0x34, 0x34, 0x34, 0x1F, 0x27, 0x39, 0x3D, 0x38, 0x32,
    0x3C, 0x2E, 0x33, 0x34, 0x32, 0xFF, 0xD9
)
[System.IO.File]::WriteAllBytes($jpgPath, $jpgBytes)
$origJpgHash = (Get-FileHash $jpgPath -Algorithm SHA256).Hash
Write-Host "Generated JPG: $jpgPath (SHA256: $origJpgHash)" -ForegroundColor Green

Write-Host "`n=== STEP 3: FETCHING EXISTING PURCHASE ORDER FOR TESTING ===" -ForegroundColor Cyan
$poHeaders = @{ "Authorization" = "Bearer $ceoToken" }
$poListRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/purchase-orders" -Method Get -Headers $poHeaders
$targetPo = $poListRes.data | Where-Object { $_.remainingQuantity -gt 5 } | Select-Object -First 1
if (!$targetPo) {
    $targetPo = $poListRes.data | Select-Object -First 1
}
$poNumber = $targetPo.poNumber
$materialType = $targetPo.materialType
$supplierName = $targetPo.supplierName
Write-Host "Using PO: $poNumber (Material: $materialType, Supplier: $supplierName, Rem: $($targetPo.remainingQuantity))" -ForegroundColor Green

Write-Host "`n=== STEP 4: SUBMITTING TRANSPORTATION REQUEST WITH REAL ATTACHMENTS ===" -ForegroundColor Cyan
$txNum = Get-Random -Minimum 10000 -Maximum 99999
$transClient = New-Object System.Net.Http.HttpClient
$transClient.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $ceoToken)

$multipart = New-Object System.Net.Http.MultipartFormDataContent

$fields = @{
    "PONumber" = $poNumber
    "TransactionNumber" = $txNum.ToString()
    "TransactionDate" = (Get-Date).ToString("o")
    "MaterialType" = "powder"
    "CarNumber" = "TRK-9876"
    "DriverName" = "Ahmed Mahmoud"
    "DriverPhone" = "01011223344"
    "Source" = "Quarry A"
    "Destination" = "Factory 1"
    "CompanyName" = "United Quarries"
    "InternalWeighbridgeCode" = "INT-WB-991"
    "ExternalWeighbridgeCode" = "EXT-WB-991"
    "ExpectedWeight" = "45.000"
    "WeightBeforeLoading" = "15.000"
    "WeightWhenEnteringFactory" = "15.000"
    "WeightWhenExitingFactory" = "45.000"
    "InternalWeight" = "30.000"
    "ExternalLoadedWeight" = "45.000"
    "ExternalEmptyWeight" = "15.000"
    "ExternalNetWeight" = "30.000"
    "NetWeight" = "30.000"
    "WasteQuantity" = "0.000"
    "Ikramiya" = "150.00"
    "CustodyAmount" = "500.00"
    "ShippingSupervisor" = "محمد ناجح"
    "DepartmentSupervisor" = "ازهر محمد"
    "ReceivingStatus" = "safe"
    "RecipientName" = "Storekeeper Ali"
    "RecipientTitle" = "Storekeeper"
    "Notes" = "Real attachment flow verification test"
    "InternalLoadedWeightTime" = (Get-Date).AddHours(-2).ToString("o")
    "ExternalLoadedWeightTime" = (Get-Date).AddHours(-1).ToString("o")
}

foreach ($k in $fields.Keys) {
    $strVal = [string]$fields[$k]
    $sc = [System.Net.Http.StringContent]::new($strVal)
    $multipart.Add($sc, $k)
}

# Section 1: Weighbridge (PDF)
$pdfFileBytes = [System.IO.File]::ReadAllBytes($pdfPath)
$pdfContentHttp = [System.Net.Http.ByteArrayContent]::new($pdfFileBytes)
$pdfContentHttp.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new("application/pdf")
$multipart.Add($pdfContentHttp, "WeighbridgeAttachment", "official_weighbridge.pdf")

# Section 2: Daily Supply (JPG)
$jpgFileBytes = [System.IO.File]::ReadAllBytes($jpgPath)
$jpgContentHttp = [System.Net.Http.ByteArrayContent]::new($jpgFileBytes)
$jpgContentHttp.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new("image/jpeg")
$multipart.Add($jpgContentHttp, "DailySupplyAttachment", "truck_scale_photo.jpg")

# Section 3: Bill of Lading (PDF)
$bolFileBytes = [System.IO.File]::ReadAllBytes($pdfPath)
$bolContentHttp = [System.Net.Http.ByteArrayContent]::new($bolFileBytes)
$bolContentHttp.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new("application/pdf")
$multipart.Add($bolContentHttp, "BillOfLadingAttachment", "official_weighbridge.pdf")

$transResp = $transClient.PostAsync("$baseUrl/api/transportation", $multipart).Result
$transRespStr = $transResp.Content.ReadAsStringAsync().Result
$transResJson = $transRespStr | ConvertFrom-Json

if ($transResp.IsSuccessStatusCode -and $transResJson.success) {
    $newTransId = $transResJson.data.id
    Write-Host "PASS: Transportation request submitted! ID: $newTransId" -ForegroundColor Green
} else {
    Write-Host "FAIL: Transportation submission failed: $transRespStr" -ForegroundColor Red
    exit 1
}

Write-Host "`n=== STEP 5: VERIFYING IN CONTROL & MONITORING ===" -ForegroundColor Cyan
$cmHeaders = @{ "Authorization" = "Bearer $cmToken" }
$cmDetailRes = Invoke-RestMethod -Uri "$baseUrl/api/control-monitoring/forms/transportation/$newTransId" -Method Get -Headers $cmHeaders
Write-Host "Retrieved C&M Details for Form #$($newTransId):" -ForegroundColor Green
Write-Host "Status: $($cmDetailRes.data.status)"
Write-Host "Attachments Count: $($cmDetailRes.data.attachments.Count)"

foreach ($att in $cmDetailRes.data.attachments) {
    Write-Host "  -> Attachment: $($att.title) | File: $($att.fileName) | Size: $($att.fileSize) bytes | ViewUrl: $($att.viewUrl)"
}

Write-Host "`n=== STEP 6: TESTING PREVIEW & DOWNLOAD OF UPLOADED ATTACHMENTS ===" -ForegroundColor Cyan
foreach ($att in $cmDetailRes.data.attachments) {
    $attUrl = "$baseUrl$($att.viewUrl)?token=$cmToken"
    $dlUrl = "$baseUrl$($att.downloadUrl)&token=$cmToken"

    # Test Preview
    $req = [System.Net.HttpWebRequest]::Create($attUrl)
    $req.Method = "GET"
    $resp = $req.GetResponse()
    $pStatus = [int]$resp.StatusCode
    $pDisp = $resp.Headers["Content-Disposition"]
    $pType = $resp.Headers["Content-Type"]
    $pLen = $resp.ContentLength
    $resp.Close()

    Write-Host "PREVIEW [$($att.key)]: Status=$pStatus, Type=$pType, Size=$pLen, Disp=$pDisp" -ForegroundColor Green

    # Test Download & Verify File Integrity
    $dlFile = Join-Path $scratchDir ("downloaded_" + $att.key + "_" + $att.fileName)
    Invoke-WebRequest -Uri $dlUrl -OutFile $dlFile -Headers $cmHeaders
    $dlHash = (Get-FileHash $dlFile -Algorithm SHA256).Hash

    $expectedHash = if ($att.fileName -like "*.pdf") { $origPdfHash } else { $origJpgHash }

    if ($dlHash -eq $expectedHash) {
        Write-Host "VERIFIED INTEGRITY [$($att.key)]: SHA256 MATCHES ORIGINAL 100%! ($dlHash)" -ForegroundColor Green
    } else {
        Write-Host "CORRUPTION DETECTED [$($att.key)]: Hash mismatch! Got $dlHash, expected $expectedHash" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`n=== ALL TESTS PASSED: UPLOAD -> STORAGE -> DB -> API -> C&M -> PREVIEW -> DOWNLOAD FLOW VERIFIED 100% ===" -ForegroundColor Green
