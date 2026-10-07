$cmLogin = Invoke-RestMethod -Uri "http://localhost:5124/api/auth/login" -Method Post -Body (@{
    identifier = "control_monitoring"
    password = "Password123!"
} | ConvertTo-Json) -ContentType "application/json"
$cmToken = $cmLogin.data.token
$cmHeaders = @{ Authorization = "Bearer $cmToken" }

# Fetch detail for item 29
$res = Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms/storage/29" -Method Get -Headers $cmHeaders
Write-Host "Success: $($res.success)"
Write-Host "Item ID: $($res.data.id)"
Write-Host "FormType: $($res.data.formType)"
Write-Host "Status: $($res.data.status)"
Write-Host "RejectionReason: $($res.data.rejectionReason)"
Write-Host "AuditTrail count: $($res.data.auditTrail.Count)"
$res.data.auditTrail | Format-Table id, formType, formId, action, actorName, comment, timestamp
