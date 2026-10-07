$baseUrl = "http://localhost:5124"

Write-Host "=== 1. AUTHENTICATING AS CONTROL_MONITORING ===" -ForegroundColor Cyan
$loginBody = @{
    Identifier = "control_monitoring"
    Password = "Password123!"
} | ConvertTo-Json

$loginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginRes.data.token
Write-Host "Login Success: $($loginRes.success), Role: $($loginRes.data.user.role)" -ForegroundColor Green

Write-Host "`n=== 2. GETTING CONTROL & MONITORING FORMS ===" -ForegroundColor Cyan
$headers = @{ "Authorization" = "Bearer $token" }
$formsRes = Invoke-RestMethod -Uri "$baseUrl/api/control-monitoring/forms" -Method Get -Headers $headers
Write-Host "Fetched $($formsRes.data.Count) forms." -ForegroundColor Green

$transForm = $formsRes.data | Where-Object { $_.formType -eq "Transportation" -and $_.attachmentCount -gt 0 } | Select-Object -First 1
$storageForm = $formsRes.data | Where-Object { $_.formType -eq "Storage" -and $_.attachmentCount -gt 0 } | Select-Object -First 1

Write-Host "Sample Transportation Form ID: $($transForm.id), Attachments: $($transForm.attachments.Count)"
Write-Host "Sample Storage Form ID: $($storageForm.id), Attachments: $($storageForm.attachments.Count)"

Write-Host "`n=== 3. TESTING TRANSPORTATION ATTACHMENT PREVIEW (INLINE) ===" -ForegroundColor Cyan
$att1 = $transForm.attachments[0]
Write-Host "Attachment: Key=$($att1.key), FileName=$($att1.fileName), ViewUrl=$($att1.viewUrl)"
$previewUrl = "$baseUrl$($att1.viewUrl)?token=$token"

$webReq = [System.Net.HttpWebRequest]::Create($previewUrl)
$webReq.Method = "GET"
$webResp = $webReq.GetResponse()
$status = [int]$webResp.StatusCode
$cType = $webResp.Headers["Content-Type"]
$cDisp = $webResp.Headers["Content-Disposition"]
$len = $webResp.ContentLength
$webResp.Close()

Write-Host "Preview Response: Status=$status, Content-Type=$cType, Content-Length=$len, Content-Disposition=$cDisp" -ForegroundColor Green
if ($status -eq 200 -and $cDisp -like "inline*") {
    Write-Host "PASS: Transportation Preview is returning 200 OK inline!" -ForegroundColor Green
} else {
    Write-Host "FAIL: Unexpected preview response" -ForegroundColor Red
}

Write-Host "`n=== 4. TESTING TRANSPORTATION ATTACHMENT DOWNLOAD ===" -ForegroundColor Cyan
$downloadUrl = "$baseUrl$($att1.downloadUrl)&token=$token"
$webReq2 = [System.Net.HttpWebRequest]::Create($downloadUrl)
$webReq2.Method = "GET"
$webResp2 = $webReq2.GetResponse()
$status2 = [int]$webResp2.StatusCode
$cType2 = $webResp2.Headers["Content-Type"]
$cDisp2 = $webResp2.Headers["Content-Disposition"]
$len2 = $webResp2.ContentLength
$webResp2.Close()

Write-Host "Download Response: Status=$status2, Content-Type=$cType2, Content-Length=$len2, Content-Disposition=$cDisp2" -ForegroundColor Green
if ($status2 -eq 200 -and $cDisp2 -like "attachment*") {
    Write-Host "PASS: Transportation Download is returning 200 OK attachment with filename!" -ForegroundColor Green
} else {
    Write-Host "FAIL: Unexpected download response" -ForegroundColor Red
}

Write-Host "`n=== 5. TESTING STORAGE ATTACHMENT PREVIEW & DOWNLOAD ===" -ForegroundColor Cyan
if ($storageForm -and $storageForm.attachments.Count -gt 0) {
    $sAtt = $storageForm.attachments[0]
    Write-Host "Storage Attachment: Key=$($sAtt.key), FileName=$($sAtt.fileName), ViewUrl=$($sAtt.viewUrl)"
    $sPreviewUrl = "$baseUrl$($sAtt.viewUrl)?token=$token"
    $sReq = [System.Net.HttpWebRequest]::Create($sPreviewUrl)
    $sResp = $sReq.GetResponse()
    Write-Host "Storage Preview: Status=$([int]$sResp.StatusCode), Content-Type=$($sResp.Headers['Content-Type']), Length=$($sResp.ContentLength)" -ForegroundColor Green
    $sResp.Close()
}

Write-Host "`n=== 6. TESTING SECURITY: UNAUTHENTICATED REQUEST ===" -ForegroundColor Cyan
try {
    $unauthReq = [System.Net.HttpWebRequest]::Create("$baseUrl$($att1.viewUrl)")
    $unauthResp = $unauthReq.GetResponse()
    Write-Host "FAIL: Should have rejected unauthenticated request" -ForegroundColor Red
    $unauthResp.Close()
} catch [System.Net.WebException] {
    $resp = $_.Exception.Response
    Write-Host "PASS: Unauthenticated request rejected with HTTP $([int]$resp.StatusCode)" -ForegroundColor Green
}

Write-Host "`n=== 7. TESTING SECURITY: PATH TRAVERSAL ATTEMPT ===" -ForegroundColor Cyan
try {
    $travUrl = "$baseUrl/api/control-monitoring/attachments/transportation/..%2F..%2Fappsettings.json?token=$token"
    $travReq = [System.Net.HttpWebRequest]::Create($travUrl)
    $travResp = $travReq.GetResponse()
    Write-Host "FAIL: Should have blocked path traversal!" -ForegroundColor Red
    $travResp.Close()
} catch [System.Net.WebException] {
    $resp = $_.Exception.Response
    Write-Host "PASS: Path traversal attempt blocked with HTTP $([int]$resp.StatusCode)" -ForegroundColor Green
}

Write-Host "`nALL ATTACHMENT API VERIFICATIONS COMPLETE." -ForegroundColor Cyan
