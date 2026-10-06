$ErrorActionPreference = 'Stop'

function Test-UserAccess($email, $pwd, $expectedAllowed) {
    $loginBody = @{ identifier = $email; password = $pwd } | ConvertTo-Json
    $loginRes = Invoke-RestMethod -Uri 'http://localhost:5124/api/auth/login' -Method Post -Body $loginBody -ContentType 'application/json'
    $token = $loginRes.data.token
    $headers = @{ Authorization = "Bearer $token" }

    try {
        $res = Invoke-RestMethod -Uri 'http://localhost:5124/api/management/overview' -Method Get -Headers $headers
        if ($expectedAllowed) {
            Write-Host "PASS: User $email accessed /api/management/overview successfully (Status 200)." -ForegroundColor Green
        } else {
            Write-Host "FAIL: User $email should have been forbidden but got 200 OK!" -ForegroundColor Red
        }
    } catch {
        $status = $_.Exception.Response.StatusCode.value__
        if (-not $expectedAllowed -and $status -eq 403) {
            Write-Host "PASS: User $email was correctly denied access with 403 Forbidden." -ForegroundColor Green
        } else {
            Write-Host "STATUS $status for $email (expectedAllowed: $expectedAllowed): $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}

Write-Host "--- TESTING AUTHORIZATION POLICIES ---"
Test-UserAccess 'kareem.ceo@facestone.com' 'Password123!' $true
Test-UserAccess 'it@facestone.com' 'Password123!' $true
Test-UserAccess 'sarah@facestone.com' 'Password123!' $false
Test-UserAccess 'transport@facestone.com' 'Password123!' $false
