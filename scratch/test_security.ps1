$baseUrl = "http://127.0.0.1:5124"

function Test-UserAccess($email, $pwd, $expectedRole, $shouldPass) {
    try {
        $loginBody = @{ identifier = $email; password = $pwd } | ConvertTo-Json
        $loginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
        $token = $loginRes.data.token
        $role = $loginRes.data.user.role
        
        $headers = @{ Authorization = "Bearer $token" }
        $res = Invoke-RestMethod -Uri "$baseUrl/api/management/overview" -Method Get -Headers $headers
        if ($shouldPass) {
            Write-Host " [PASS] ${email} ($role): Access GRANTED (200 OK) as expected."
        } else {
            Write-Host " [FAIL] ${email} ($role): Access was GRANTED but expected 403 Forbidden!"
        }
    } catch {
        $status = $_.Exception.Response.StatusCode.value__
        if (!$shouldPass -and ($status -eq 403 -or $status -eq 401)) {
            Write-Host " [PASS] ${email}: Access DENIED ($status) as expected."
        } else {
            Write-Host " [UNEXPECTED] ${email}: Encountered status $status."
        }
    }
}

Write-Host "--- TESTING AUTHORIZATION ENFORCEMENT ---"
# CEO (Should Pass)
Test-UserAccess "kareem.ceo@facestone.com" "Password123!" "CEO" $true

# IT (Should Pass)
Test-UserAccess "it@facestone.com" "Password123!" "IT" $true

# Accountant (Should Fail 403)
Test-UserAccess "sarah@facestone.com" "Password123!" "Accountant" $false

# Transportation Manager (Should Fail 403)
Test-UserAccess "transport@facestone.com" "Password123!" "TransportationManager" $false

# Anonymous (Should Fail 401)
try {
    $res = Invoke-RestMethod -Uri "$baseUrl/api/management/overview" -Method Get
    Write-Host " [FAIL] Anonymous access was granted!"
} catch {
    $status = $_.Exception.Response.StatusCode.value__
    Write-Host " [PASS] Anonymous: Access DENIED ($status) as expected."
}
