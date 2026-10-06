$baseUrl = "http://127.0.0.1:5124"

# 1. Login CEO
$loginBody = @{ identifier = "kareem.ceo@facestone.com"; password = "Password123!" } | ConvertTo-Json
$loginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginRes.data.token
$headers = @{ Authorization = "Bearer $token" }

# 2. Get current overview
$before = Invoke-RestMethod -Uri "$baseUrl/api/management/overview" -Method Get -Headers $headers
$tripsBefore = $before.data.transportation.tripsCount
$qtyBefore = $before.data.transportation.totalReceivedQuantity
Write-Host "BEFORE: Trips = $tripsBefore, TotalQty = $qtyBefore"

# 3. Add a new Transportation record
$newTrans = @{
    TransactionNumber = 9988
    TransactionDate = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
    CarNumber = "9988-TEST"
    DriverName = "Test Driver Realtime"
    Source = "Sinai Quarry"
    PONumber = "TEST-PO-001"
    MaterialType = "Aggregate Grade 1"
    WeightBeforeLoading = 10.0
    WeightWhenEnteringFactory = 45.0
    WeightWhenExitingFactory = 15.0
    ExpectedWeight = 30.0
    NetWeight = 30.0
    Destination = "Central Plant"
    TransactionStatus = "Completed"
    ReceivingStatus = "Received"
    Notes = "Real-time live update verification test"
    DateOfSubmit = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
} | ConvertTo-Json

$postRes = Invoke-RestMethod -Uri "$baseUrl/api/transportation" -Method Post -Body $newTrans -ContentType "application/json" -Headers $headers
Write-Host "Created test transportation record: $($postRes.message)"

# 4. Get updated overview
$after = Invoke-RestMethod -Uri "$baseUrl/api/management/overview" -Method Get -Headers $headers
$tripsAfter = $after.data.transportation.tripsCount
$qtyAfter = $after.data.transportation.totalReceivedQuantity
Write-Host "AFTER: Trips = $tripsAfter, TotalQty = $qtyAfter"

if ($tripsAfter -eq ($tripsBefore + 1) -and $qtyAfter -eq ($qtyBefore + 30.0)) {
    Write-Host "[SUCCESS] REAL-TIME REFRESH VERIFIED: Management data changed dynamically without manual cache or hardcoding!"
} else {
    Write-Host "[INFO] Trips before: $tripsBefore, after: $tripsAfter, Qty before: $qtyBefore, after: $qtyAfter"
}
