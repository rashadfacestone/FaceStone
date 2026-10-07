$baseUrl = "http://localhost:5124"
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

Write-Host "=== TESTING STORAGE ATTACHMENT FLOW ===" -ForegroundColor Cyan
$ceoLogin = @{ Identifier = "kareem_ceo"; Password = "Password123!" } | ConvertTo-Json
$ceoRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $ceoLogin -ContentType "application/json"
$ceoToken = $ceoRes.data.token

$cmLogin = @{ Identifier = "control_monitoring"; Password = "Password123!" } | ConvertTo-Json
$cmRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $cmLogin -ContentType "application/json"
$cmToken = $cmRes.data.token

$scratchDir = "f:\FaceStoneWebsite\scratch\test_att"
$pdfPath = Join-Path $scratchDir "official_weighbridge.pdf"
$origPdfHash = (Get-FileHash $pdfPath -Algorithm SHA256).Hash

# Fetch PO
$poHeaders = @{ "Authorization" = "Bearer $ceoToken" }
$poListRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/purchase-orders" -Method Get -Headers $poHeaders
$targetPo = $poListRes.data | Select-Object -First 1
$poNumber = $targetPo.poNumber

# Submit Storage Transaction
$client = New-Object System.Net.Http.HttpClient
$client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $ceoToken)

$multipart = New-Object System.Net.Http.MultipartFormDataContent
$mNum = Get-Random -Minimum 50000 -Maximum 99999

$fields = @{
    "PONumber" = $poNumber
    "MovementNumber" = $mNum.ToString()
    "TransactionDate" = (Get-Date).ToString("o")
    "Action" = "import"
    "Material" = "powder"
    "StorageLocation" = "Yard A"
    "IncomingQuantity" = "30.000"
    "OpeningBalance" = "50.000"
    "ClosingBalance" = "80.000"
    "InternalLoadedWeight" = "45.000"
    "InternalEmptyWeight" = "15.000"
    "InternalLoadedWeightTime" = (Get-Date).ToString("o")
    "InternalWeighbridgeReference" = "WB-STORE-001"
    "ResponsibleEmployee" = $ceoRes.data.user.fullName
    "ReceivingEmployee" = $ceoRes.data.user.fullName
    "Notes" = "Storage attachment flow verification test"
}

foreach ($k in $fields.Keys) {
    $strVal = [string]$fields[$k]
    $sc = [System.Net.Http.StringContent]::new($strVal)
    $multipart.Add($sc, $k)
}

$pdfFileBytes = [System.IO.File]::ReadAllBytes($pdfPath)
$pdfContentHttp = [System.Net.Http.ByteArrayContent]::new($pdfFileBytes)
$pdfContentHttp.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new("application/pdf")
$multipart.Add($pdfContentHttp, "WeighbridgeAttachment", "official_storage_receipt.pdf")

$resp = $client.PostAsync("$baseUrl/api/storage", $multipart).Result
$respStr = $resp.Content.ReadAsStringAsync().Result
$resJson = $respStr | ConvertFrom-Json

if ($resp.IsSuccessStatusCode -and $resJson.success) {
    $newStorageId = $resJson.data.id
    Write-Host "PASS: Storage request submitted! ID: $newStorageId" -ForegroundColor Green
} else {
    Write-Host "FAIL: Storage submission failed: $respStr" -ForegroundColor Red
    exit 1
}

# Verify in C&M
$cmHeaders = @{ "Authorization" = "Bearer $cmToken" }
$cmDetailRes = Invoke-RestMethod -Uri "$baseUrl/api/control-monitoring/forms/storage/$newStorageId" -Method Get -Headers $cmHeaders
Write-Host "Retrieved C&M Storage Details for Form #${newStorageId}:" -ForegroundColor Green
Write-Host "Attachments Count: $($cmDetailRes.data.attachments.Count)"

$att = $cmDetailRes.data.attachments[0]
Write-Host "  -> Storage Attachment: $($att.title) | File: $($att.fileName) | ViewUrl: $($att.viewUrl)"

$attUrl = "$baseUrl$($att.viewUrl)?token=$cmToken"
$dlUrl = "$baseUrl$($att.downloadUrl)&token=$cmToken"

$req = [System.Net.HttpWebRequest]::Create($attUrl)
$req.Method = "GET"
$r = $req.GetResponse()
Write-Host "PREVIEW: Status=$([int]$r.StatusCode), Type=$($r.Headers['Content-Type']), Size=$($r.ContentLength)" -ForegroundColor Green
$r.Close()

$dlFile = Join-Path $scratchDir ("downloaded_storage_" + $att.fileName)
Invoke-WebRequest -Uri $dlUrl -OutFile $dlFile -Headers $cmHeaders
$dlHash = (Get-FileHash $dlFile -Algorithm SHA256).Hash

if ($dlHash -eq $origPdfHash) {
    Write-Host "VERIFIED STORAGE ATTACHMENT INTEGRITY: SHA256 MATCHES 100%! ($dlHash)" -ForegroundColor Green
} else {
    Write-Host "CORRUPTION DETECTED: Hash mismatch! Got $dlHash, expected $origPdfHash" -ForegroundColor Red
    exit 1
}

Write-Host "=== STORAGE ATTACHMENT TEST PASSED 100% ===" -ForegroundColor Green
