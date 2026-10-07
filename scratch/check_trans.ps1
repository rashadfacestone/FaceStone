$cmLogin = Invoke-RestMethod -Uri "http://localhost:5124/api/auth/login" -Method Post -Body (@{
    identifier = "control_monitoring"
    password = "Password123!"
} | ConvertTo-Json) -ContentType "application/json"
$cmToken = $cmLogin.data.token
$cmHeaders = @{ Authorization = "Bearer $cmToken" }

# Fetch detail for item 34 (Transportation)
$res = Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms/transportation/34" -Method Get -Headers $cmHeaders
Write-Host "Success: $($res.success)"
Write-Host "Item ID: $($res.data.id)"
Write-Host "Attachments count: $($res.data.attachments.Count)"

$firstAtt = $res.data.attachments[0]
Write-Host "Testing attachment: $($firstAtt.fileName) via $($firstAtt.viewUrl)"
$attRes = Invoke-WebRequest -Uri "http://localhost:5124$($firstAtt.viewUrl)" -Method Get -Headers $cmHeaders
Write-Host "Attachment response Status: $($attRes.StatusCode) | Content-Type: $($attRes.Headers['Content-Type']) | Length: $($attRes.RawContentLength) bytes"
