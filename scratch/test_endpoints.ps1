$ErrorActionPreference = "Stop"

$loginBody = @{
    identifier = "control_monitoring"
    password = "Password123!"
} | ConvertTo-Json

$loginRes = Invoke-RestMethod -Uri "http://localhost:5124/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginRes.data.token
$user = $loginRes.data.user
Write-Host "Login successful: $($user.fullName) | Role: $($user.role)"

$headers = @{
    Authorization = "Bearer $token"
}

$formsRes = Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms" -Method Get -Headers $headers
$forms = $formsRes.data
Write-Host "Total review items returned: $($forms.Count)"

$trans = $forms | Where-Object { $_.formType -eq "Transportation" } | Select-Object -First 1
if ($trans) {
    Write-Host "`n--- TRANSPORTATION ITEM ---"
    Write-Host "ID: $($trans.id), PO: $($trans.poNumber), Transaction: $($trans.transactionNumber), Status: $($trans.status), Attachments: $($trans.attachmentCount)"
    
    $dRes = Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms/$($trans.id)?formType=Transportation" -Method Get -Headers $headers
    $detail = $dRes.data
    Write-Host "Detail response -> Driver: $($detail.driverName), NetWeight: $($detail.netWeight), ExpectedWeight: $($detail.expectedWeight), Attachments count: $($detail.attachments.Count), Timeline events: $($detail.auditTimeline.Count)"
}

$storage = $forms | Where-Object { $_.formType -eq "Storage" } | Select-Object -First 1
if ($storage) {
    Write-Host "`n--- STORAGE ITEM ---"
    Write-Host "ID: $($storage.id), PO: $($storage.poNumber), Transaction: $($storage.transactionNumber), Status: $($storage.status), Attachments: $($storage.attachmentCount)"
    
    $sRes = Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms/$($storage.id)?formType=Storage" -Method Get -Headers $headers
    $sDetail = $sRes.data
    Write-Host "Storage detail -> TransactionType: $($sDetail.transactionType), StoreLocation: $($sDetail.storeLocation), Attachments: $($sDetail.attachments.Count), Timeline: $($sDetail.auditTimeline.Count)"
}

# Test direct single ID route fallback
if ($trans) {
    $singleRes = Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms/$($trans.id)" -Method Get -Headers $headers
    $singleRoute = $singleRes.data
    Write-Host "`nSingle ID fallback route test successful: ID $($singleRoute.id), Type: $($singleRoute.formType)"
}

# Test SPA direct subpath route with Accept: text/html
$htmlHeaders = @{
    Accept = "text/html"
}
$spaRes = Invoke-WebRequest -Uri "http://localhost:5124/control-monitoring/request/transportation/1" -Method Get -Headers $htmlHeaders
Write-Host "`nDirect SPA route /control-monitoring/request/transportation/1 StatusCode: $($spaRes.StatusCode) (Content-Length: $($spaRes.Content.Length) bytes)"
