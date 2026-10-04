$baseUrl = "http://localhost:5124"
$loginBody = @{ identifier = "transport@facestone.com"; password = "Password123!" } | ConvertTo-Json
$loginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$headers = @{ "Authorization" = "Bearer $($loginRes.data.token)" }
$po = Invoke-RestMethod -Uri "$baseUrl/api/transportation/po/PO-2026-9810" -Method Get -Headers $headers
$po | ConvertTo-Json
