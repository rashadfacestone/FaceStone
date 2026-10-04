$baseUrl = "http://localhost:5124"
$accBody = @{ identifier = "sarah@facestone.com"; password = "Password123!" } | ConvertTo-Json
$accLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $accBody -ContentType "application/json"
$headers = @{ "Authorization" = "Bearer $($accLogin.data.token)" }
$mgmtData = Invoke-RestMethod -Uri "$baseUrl/api/finance/management-data?pageSize=50" -Method Get -Headers $headers
foreach ($r in $mgmtData.data.records) {
    Write-Host "$($r.poNumber) | Supplier: $($r.supplier) | RemQty: $($r.remainingQuantity)"
}
