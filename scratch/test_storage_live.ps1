$baseUrl = "http://127.0.0.1:5124"

# 1. Login CEO
$loginBody = @{ identifier = "kareem.ceo@facestone.com"; password = "Password123!" } | ConvertTo-Json
$loginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginRes.data.token
$headers = @{ Authorization = "Bearer $token" }

# 2. Get Overview Before
$before = Invoke-RestMethod -Uri "$baseUrl/api/management/overview" -Method Get -Headers $headers
$depositBefore = $before.data.storage.totalDeposit
Write-Host "BEFORE: Storage Deposit = $depositBefore"

# 3. Add Storage transaction
$storageForm = @{
    material = "سن وركام خرساني تدرج 1"
    action = "import"
    incomingQuantity = "50.000"
    transactionDate = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
    internalWeighbridgeReference = "WB-LIVE-TEST-01"
    internalLoadedWeight = "70.000"
    internalEmptyWeight = "20.000"
    internalLoadedWeightTime = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
    responsibleEmployee = "عادل مراد"
    notes = "Real-time Live Storage Update Test"
}

# Use multipart form or API
$boundary = [System.Guid]::NewGuid().ToString()
$LF = "`r`n"
$bodyLines = [System.Collections.Generic.List[string]]::new()

foreach ($key in $storageForm.Keys) {
    $bodyLines.Add("--$boundary")
    $bodyLines.Add("Content-Disposition: form-data; name=`"$key`"$LF")
    $bodyLines.Add($storageForm[$key])
}
$bodyLines.Add("--$boundary--$LF")
$body = $bodyLines -join "$LF"

$postRes = Invoke-RestMethod -Uri "$baseUrl/api/storage" -Method Post -Headers @{
    Authorization = "Bearer $token"
    "Content-Type" = "multipart/form-data; boundary=$boundary"
} -Body $body

Write-Host "Saved storage transaction: $($postRes.message)"

# 4. Get Overview After
$after = Invoke-RestMethod -Uri "$baseUrl/api/management/overview" -Method Get -Headers $headers
$depositAfter = $after.data.storage.totalDeposit
Write-Host "AFTER: Storage Deposit = $depositAfter"

if ($depositAfter -eq ($depositBefore + 50.0)) {
    Write-Host "[SUCCESS] REAL-TIME UPDATE TEST PASSED: Adding Storage movement immediately updated Management Overview!"
} else {
    Write-Host "[INFO] Deposit before: $depositBefore, after: $depositAfter"
}
