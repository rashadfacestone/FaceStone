$smLogin = Invoke-RestMethod -Uri "http://localhost:5124/api/auth/login" -Method Post -Body (@{ identifier = "storage_manager"; password = "Storage123!" } | ConvertTo-Json) -ContentType "application/json"
$smToken = $smLogin.data.token
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
    responsibleEmployee = "Mahmoud Adel"
    notes = "New limestone powder batch for Silo 2"
} | ConvertTo-Json

try {
    $res = Invoke-RestMethod -Uri "http://localhost:5124/api/storage" -Method Post -Body $storageBody -Headers @{ Authorization = "Bearer $smToken" } -ContentType "application/json"
} catch {
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    Write-Host "Server response:" ($reader.ReadToEnd())
}
