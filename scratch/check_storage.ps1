$ErrorActionPreference = "Stop"

$loginBody = @{
    identifier = "storage_manager"
    password = "Storage123!"
} | ConvertTo-Json

$loginRes = Invoke-RestMethod -Uri "http://localhost:5124/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginRes.data.token
Write-Host "Storage manager login successful: $($loginRes.data.user.fullName)"

$headers = @{
    Authorization = "Bearer $token"
}

$storageList = Invoke-RestMethod -Uri "http://localhost:5124/api/storage/transactions" -Method Get -Headers $headers
Write-Host "Total storage transactions: $($storageList.data.Count)"
if ($storageList.data.Count -gt 0) {
    $first = $storageList.data[0]
    Write-Host "First storage item: ID=$($first.id), PO=$($first.poNumber), Status=$($first.approvalStatus)"
}
