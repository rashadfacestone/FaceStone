$login = Invoke-RestMethod -Uri 'http://localhost:5124/api/auth/login' -Method Post -Body (@{ identifier = 'kareem_ceo'; password = 'Password123!' } | ConvertTo-Json) -ContentType 'application/json'
$headers = @{ 'Authorization' = 'Bearer ' + $login.data.token }
$res = Invoke-RestMethod -Uri 'http://localhost:5124/api/finance/expenses' -Headers $headers -Method Get
Write-Host ($res.data | ConvertTo-Json -Depth 5)
