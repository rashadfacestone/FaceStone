$res = Invoke-RestMethod -Uri 'http://localhost:5124/api/auth/login' -Method Post -Body (@{ Identifier = 'kareem_ceo'; Password = 'Password123!' } | ConvertTo-Json) -ContentType 'application/json'
$t = $res.data.token
$pos = (Invoke-RestMethod -Uri 'http://localhost:5124/api/finance/purchase-orders' -Headers @{ Authorization = "Bearer $t" }).data
$pos | Select-Object -First 5 poNumber, materialType, remainingQuantity | Format-Table
