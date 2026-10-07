$ErrorActionPreference = "Stop"

$cmLogin = Invoke-RestMethod -Uri "http://localhost:5124/api/auth/login" -Method Post -Body (@{
    identifier = "control_monitoring"
    password = "Password123!"
} | ConvertTo-Json) -ContentType "application/json"
$cmToken = $cmLogin.data.token

$headers = @{
    Authorization = "Bearer $cmToken"
}

$stId = 26
$stDetail = (Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms/$($stId)?formType=Storage" -Method Get -Headers $headers).data

Write-Host "ID: $($stDetail.id)"
Write-Host "FormType: $($stDetail.formType)"
Write-Host "Material: $($stDetail.material)"
Write-Host "Action: $($stDetail.action)"
Write-Host "StorageInternalNetWeight: $($stDetail.storageInternalNetWeight)"
Write-Host "StorageInternalLoadedWeight: $($stDetail.storageInternalLoadedWeight)"
Write-Host "StorageInternalEmptyWeight: $($stDetail.storageInternalEmptyWeight)"
Write-Host "StorageLocation: $($stDetail.storageLocation)"
Write-Host "Responsible: $($stDetail.responsibleEmployee)"
Write-Host "Status: $($stDetail.status)"
Write-Host "Attachments: $($stDetail.attachments.Count)"
Write-Host "Audit Timeline: $($stDetail.auditTimeline.Count)"
