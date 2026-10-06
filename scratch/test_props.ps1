$ErrorActionPreference = 'Stop'
$loginBody = @{ identifier = 'kareem.ceo@facestone.com'; password = 'Password123!' } | ConvertTo-Json
$loginRes = Invoke-RestMethod -Uri 'http://localhost:5124/api/auth/login' -Method Post -Body $loginBody -ContentType 'application/json'
$headers = @{ Authorization = "Bearer $($loginRes.data.token)" }

Write-Host "--- TRANSPORTATION DATA ---"
(Invoke-RestMethod -Uri 'http://localhost:5124/api/management/transportation' -Headers $headers).data | ConvertTo-Json -Depth 2

Write-Host "--- STORAGE DATA ---"
(Invoke-RestMethod -Uri 'http://localhost:5124/api/management/storage' -Headers $headers).data | ConvertTo-Json -Depth 2

Write-Host "--- EMPLOYEES DATA ---"
(Invoke-RestMethod -Uri 'http://localhost:5124/api/management/employees' -Headers $headers).data | ConvertTo-Json -Depth 2

Write-Host "--- SUPPLIERS DATA ---"
(Invoke-RestMethod -Uri 'http://localhost:5124/api/management/suppliers' -Headers $headers).data | ConvertTo-Json -Depth 2
