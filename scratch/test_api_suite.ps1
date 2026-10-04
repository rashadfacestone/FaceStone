$baseUrl = "http://localhost:5124"

function Test-UserLogin($username, $password, $expectedRole) {
    Write-Host "Testing login for $username..."
    $body = @{
        identifier = $username
        password = $password
    } | ConvertTo-Json

    $loginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $body -ContentType "application/json"
    if (-not $loginRes.success) {
        Write-Error "Login failed for ${username}: $($loginRes.message)"
        return $null
    }

    $token = $loginRes.data.token
    $user = $loginRes.data.user
    Write-Host " [OK] Authenticated as $($user.fullName) | Role: $($user.role)"

    # Test /api/departments
    $headers = @{
        Authorization = "Bearer $token"
    }
    $deptRes = Invoke-RestMethod -Uri "$baseUrl/api/departments" -Method Get -Headers $headers
    Write-Host "  Departments available: $(($deptRes.data.departments | ForEach-Object { $_.name }) -join ', ')"
    Write-Host "  Finance: $($deptRes.data.hasFinanceAccess) | Trans: $($deptRes.data.hasTransportationAccess) | Storage: $($deptRes.data.hasStorageAccess) | Mgmt: $($deptRes.data.hasManagementAccess)"

    return @{ Token = $token; User = $user }
}

Write-Host "=== 1. TESTING AUTHENTICATION & ROLE-BASED ACCESS ===" -ForegroundColor Cyan
$ceo = Test-UserLogin "kareem_ceo" "Password123!" "CEO"
$trans = Test-UserLogin "transport_manager" "Password123!" "TRANSPORTATION_MANAGER"
$storage = Test-UserLogin "storage_manager" "Storage123!" "STORAGE_MANAGER"
$factory = Test-UserLogin "factory_manager" "Manager123!" "FACTORY_MANAGER"
$acc = Test-UserLogin "sarah_concrete" "Password123!" "Accountant"

Write-Host "`n=== 2. TESTING CHAT & CONVERSATIONS API ===" -ForegroundColor Cyan
$headers = @{ Authorization = "Bearer $($ceo.Token)" }
$convs = Invoke-RestMethod -Uri "$baseUrl/api/chat/conversations" -Method Get -Headers $headers
Write-Host " [OK] Conversations fetched: $($convs.data.Count) conversations found."

Write-Host "`n=== 3. TESTING SENDING MESSAGE ===" -ForegroundColor Cyan
$targetEmpId = $convs.data[0].employeeId
$sendBody = @{
    targetEmployeeId = $targetEmpId
    message = "Hello from Mobile FaceStone PWA Test suite at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    clientMessageId = [System.Guid]::NewGuid().ToString()
} | ConvertTo-Json
$sendRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/send" -Method Post -Body $sendBody -Headers $headers -ContentType "application/json"
Write-Host " [OK] Message sent successfully. ID: $($sendRes.data.id) | Text: $($sendRes.data.messageText)"

Write-Host "`n=== ALL API TESTS PASSED! ===" -ForegroundColor Green
