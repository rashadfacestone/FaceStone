$login = Invoke-RestMethod -Uri 'http://localhost:5124/api/auth/login' -Method Post -Body (@{ identifier = 'kareem_ceo'; password = 'Password123!' } | ConvertTo-Json) -ContentType 'application/json'
$headers = @{ 'Authorization' = 'Bearer ' + $login.data.token }
$body = @{
    transactionDate = '2026-10-05T00:00:00Z'
    bankName = 'CIB'
    transactionType = 'Transfer'
    direction = 'In'
    amount = 50000.0
    referenceNumber = 'REF-123'
    description = 'Test deposit'
} | ConvertTo-Json

try {
    $res = Invoke-RestMethod -Uri 'http://localhost:5124/api/finance/bank' -Headers $headers -Method Post -Body $body -ContentType 'application/json'
    Write-Host 'SUCCESS:' ($res | ConvertTo-Json -Depth 5)
} catch {
    Write-Host 'STATUS:' $_.Exception.Response.StatusCode.value__
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    Write-Host 'BODY:' $reader.ReadToEnd()
}
