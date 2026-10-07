$ErrorActionPreference = "Stop"

$login = Invoke-RestMethod -Uri "http://localhost:5124/api/auth/login" -Method Post -Body (@{
    identifier = "control_monitoring"
    password = "Password123!"
} | ConvertTo-Json) -ContentType "application/json"
$token = $login.data.token
$headers = @{ Authorization = "Bearer $token" }

# 1. Fetch transportation form 34
$tRes = Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms/transportation/34" -Method Get -Headers $headers
Write-Host "--- TRANSPORTATION 34 ATTACHMENTS ---"
foreach ($att in $tRes.data.attachments) {
    Write-Host "Title: $($att.title) | File: $($att.fileName) | ViewUrl: $($att.viewUrl)"
}

# 2. Check physical files on disk in wwwroot/uploads
$webRoot = "f:\FaceStoneWebsite\wwwroot"
Write-Host "`n--- PHYSICAL FILES ON DISK ---"
Get-ChildItem -Recurse "$webRoot\uploads" | Format-Table FullName, Length

# 3. Test calling the endpoint directly with Bearer token vs without Bearer token
$firstAtt = $tRes.data.attachments[0]
Write-Host "`n--- TESTING ATTACHMENT FETCH ---"
Write-Host "Testing WITH Bearer Token:"
try {
    $withAuth = Invoke-WebRequest -Uri "http://localhost:5124$($firstAtt.viewUrl)" -Method Get -Headers $headers
    Write-Host "Status with Auth: $($withAuth.StatusCode) ContentType: $($withAuth.Headers['Content-Type']) Length: $($withAuth.RawContentLength)"
} catch {
    Write-Host "Error with Auth: $($_.Exception.Message)"
}

Write-Host "`nTesting WITHOUT Bearer Token (Simulating browser <a> or <img src>):"
try {
    $noAuth = Invoke-WebRequest -Uri "http://localhost:5124$($firstAtt.viewUrl)" -Method Get
    Write-Host "Status without Auth: $($noAuth.StatusCode)"
} catch {
    Write-Host "Error without Auth: $($_.Exception.Message)"
}
