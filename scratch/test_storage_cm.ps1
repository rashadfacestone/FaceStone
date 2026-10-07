$ErrorActionPreference = "Stop"

# 1. Login as Storage Manager
$smLogin = Invoke-RestMethod -Uri "http://localhost:5124/api/auth/login" -Method Post -Body (@{
    identifier = "storage_manager"
    password = "Storage123!"
} | ConvertTo-Json) -ContentType "application/json"
$smToken = $smLogin.data.token
$empName = $smLogin.data.user.fullName
Write-Host "Using employee name: $empName"

# 2. Create a test storage transaction
$now = [DateTime]::UtcNow.ToString("o")
$storageBody = @{
    material = "powder"
    action = "import"
    incomingQuantity = 25.50
    transactionDate = $now
    internalWeighbridgeReference = "WB-INT-9901"
    internalLoadedWeight = 35.50
    internalEmptyWeight = 10.00
    internalLoadedWeightTime = $now
    responsibleEmployee = $empName
    notes = "New limestone powder batch for Silo 2"
} | ConvertTo-Json

$storageRes = Invoke-RestMethod -Uri "http://localhost:5124/api/storage" -Method Post -Body $storageBody -Headers @{ Authorization = "Bearer $smToken" } -ContentType "application/json"
Write-Host "Created storage transaction ID: $($storageRes.data.id), Movement: $($storageRes.data.movementNumber), Status: $($storageRes.data.approvalStatus)"

# 3. Login as Control & Monitoring
$cmLogin = Invoke-RestMethod -Uri "http://localhost:5124/api/auth/login" -Method Post -Body (@{
    identifier = "control_monitoring"
    password = "Password123!"
} | ConvertTo-Json) -ContentType "application/json"
$cmToken = $cmLogin.data.token

$forms = (Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms" -Method Get -Headers @{ Authorization = "Bearer $cmToken" }).data
Write-Host "`nControl & Monitoring forms count: $($forms.Count)"

foreach ($item in $forms) {
    Write-Host "-> Type: $($item.formType) | ID: $($item.id) | PO: $($item.poNumber) | Status: $($item.status) | Employee: $($item.responsibleEmployee)"
}

# 4. Verify fetching details for the newly created storage item
$stId = $storageRes.data.id
$stDetail = (Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms/$stId?formType=Storage" -Method Get -Headers @{ Authorization = "Bearer $cmToken" }).data
Write-Host "`nFetched Storage Detail via Control & Monitoring API:"
Write-Host "ID: $($stDetail.id)"
Write-Host "Material: $($stDetail.material)"
Write-Host "Transaction Type: $($stDetail.transactionType)"
Write-Host "Internal Net Weight: $($stDetail.internalNetWeight)"
Write-Host "Store Location: $($stDetail.storeLocation)"
Write-Host "Responsible: $($stDetail.responsibleEmployee)"
Write-Host "Audit Timeline events: $($stDetail.auditTimeline.Count)"
