$ErrorActionPreference = "Stop"

$cmLogin = Invoke-RestMethod -Uri "http://localhost:5124/api/auth/login" -Method Post -Body (@{
    identifier = "control_monitoring"
    password = "Password123!"
} | ConvertTo-Json) -ContentType "application/json"
$cmToken = $cmLogin.data.token
$cmHeaders = @{ Authorization = "Bearer $cmToken" }

$smLogin = Invoke-RestMethod -Uri "http://localhost:5124/api/auth/login" -Method Post -Body (@{
    identifier = "storage_manager"
    password = "Storage123!"
} | ConvertTo-Json) -ContentType "application/json"
$smToken = $smLogin.data.token
$smHeaders = @{ Authorization = "Bearer $smToken" }

# 1. Create a fresh PENDING storage item for approval testing
$now = [DateTime]::UtcNow.ToString("o")
$pendingApprove = Invoke-RestMethod -Uri "http://localhost:5124/api/storage" -Method Post -Body (@{
    material = "powder"
    action = "import"
    incomingQuantity = 18.25
    transactionDate = $now
    internalWeighbridgeReference = "WB-INT-APP-01"
    internalLoadedWeight = 28.25
    internalEmptyWeight = 10.00
    internalLoadedWeightTime = $now
    responsibleEmployee = "Storage & Factory Operations Manager"
    notes = "Fresh batch for approval verification"
} | ConvertTo-Json) -Headers $smHeaders -ContentType "application/json"

$appId = $pendingApprove.data.id
Write-Host "Created fresh pending item ID: $appId for approval test"

# 2. Approve the item
$appRes = Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms/storage/$appId/accept" -Method Post -Headers $cmHeaders
Write-Host "Accept message: $($appRes.message)"

# 3. Test duplicate approval prevention
try {
    Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms/storage/$appId/accept" -Method Post -Headers $cmHeaders
    Write-Host "FAIL: Duplicate approval allowed!"
} catch {
    Write-Host "SUCCESS: Duplicate approval blocked! Reason: $($_.Exception.Message)"
}

# 4. Create a fresh PENDING storage item for rejection testing
$pendingReject = Invoke-RestMethod -Uri "http://localhost:5124/api/storage" -Method Post -Body (@{
    material = "slurry"
    action = "export"
    outgoingQuantity = 14.50
    transactionDate = $now
    internalWeighbridgeReference = "WB-INT-REJ-01"
    internalLoadedWeight = 24.50
    internalEmptyWeight = 10.00
    internalLoadedWeightTime = $now
    responsibleEmployee = "Storage & Factory Operations Manager"
    notes = "Fresh batch for rejection verification"
} | ConvertTo-Json) -Headers $smHeaders -ContentType "application/json"

$rejId = $pendingReject.data.id
Write-Host "`nCreated fresh pending item ID: $rejId for rejection test"

# 5. Reject the item
$rejRes = Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms/storage/$rejId/reject" -Method Post -Body (@{
    reason = "Quality inspection failed: moisture level exceeds tolerance"
} | ConvertTo-Json) -Headers $cmHeaders -ContentType "application/json"
Write-Host "Reject message: $($rejRes.message)"

# 6. Test duplicate rejection prevention
try {
    Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms/storage/$rejId/reject" -Method Post -Body (@{
        reason = "Attempting duplicate rejection"
    } | ConvertTo-Json) -Headers $cmHeaders -ContentType "application/json"
    Write-Host "FAIL: Duplicate rejection allowed!"
} catch {
    Write-Host "SUCCESS: Duplicate rejection blocked! Reason: $($_.Exception.Message)"
}

# 7. Test rejecting an already approved item prevention
try {
    Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms/storage/$appId/reject" -Method Post -Body (@{
        reason = "Attempting to reject approved item"
    } | ConvertTo-Json) -Headers $cmHeaders -ContentType "application/json"
    Write-Host "FAIL: Rejecting approved item allowed!"
} catch {
    Write-Host "SUCCESS: Rejecting approved item blocked! Reason: $($_.Exception.Message)"
}

# 8. Check audit timeline for the approved and rejected items
$appDetail = (Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms/$appId?formType=Storage" -Method Get -Headers $cmHeaders).data
Write-Host "`nApproved item audit history count: $($appDetail.auditTrail.Count)"
foreach ($ev in $appDetail.auditTrail) {
    Write-Host "  -> [Action: $($ev.action)] By: $($ev.actorName) | Comment: $($ev.comment)"
}

$rejDetail = (Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/forms/$rejId?formType=Storage" -Method Get -Headers $cmHeaders).data
Write-Host "`nRejected item audit history count: $($rejDetail.auditTrail.Count)"
foreach ($ev in $rejDetail.auditTrail) {
    Write-Host "  -> [Action: $($ev.action)] By: $($ev.actorName) | Reason: $($ev.comment)"
}
