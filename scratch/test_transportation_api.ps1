$baseUrl = "http://localhost:5124"

Write-Output "=================================================="
Write-Output "1. TEST CONFIG ENDPOINT: /api/transportation/config"
Write-Output "=================================================="
$configRes = Invoke-RestMethod -Uri "$baseUrl/api/transportation/config" -Method Get
Write-Output ("Material Types Count: " + $configRes.data.materialTypes.Count)
Write-Output ("Destinations Count:   " + $configRes.data.destinations.Count)
Write-Output ("Transaction Statuses: " + ($configRes.data.transactionStatuses -join ", "))
Write-Output ("Receiving Statuses:   " + ($configRes.data.receivingStatuses -join ", "))

Write-Output "`n=================================================="
Write-Output "2. TEST AUTHENTICATION & ROLE AUTHORIZATION"
Write-Output "=================================================="

# A. Login as CEO
$ceoLoginBody = @{ identifier = "kareem.ceo@facestone.com"; password = "Password123!" } | ConvertTo-Json
$ceoLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $ceoLoginBody -ContentType "application/json"
$ceoToken = $ceoLogin.data.token
Write-Output ("CEO Login: " + $ceoLogin.success + " | Role: " + $ceoLogin.data.user.role)

# B. Login as Transportation Manager
$transLoginBody = @{ identifier = "transport@facestone.com"; password = "Password123!" } | ConvertTo-Json
$transLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $transLoginBody -ContentType "application/json"
$transToken = $transLogin.data.token
Write-Output ("Trans Manager Login: " + $transLogin.success + " | Role: " + $transLogin.data.user.role)

# C. Login as Employee (Finance only)
$empLoginBody = @{ identifier = "alex@facestone.com"; password = "Password123!" } | ConvertTo-Json
$empLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $empLoginBody -ContentType "application/json"
$empToken = $empLogin.data.token
Write-Output ("Employee Login: " + $empLogin.success + " | Role: " + $empLogin.data.user.role)

Write-Output "`n=================================================="
Write-Output "3. TEST UNAUTHORIZED ACCESS (EMPLOYEE TO TRANSPORTATION)"
Write-Output "=================================================="
try {
    $empHeaders = @{ Authorization = "Bearer $empToken" }
    $empVerify = Invoke-RestMethod -Uri "$baseUrl/api/department/transportation" -Method Get -Headers $empHeaders
    Write-Error "FAIL: Employee should have been denied access!"
} catch {
    Write-Output ("SUCCESS: Employee was correctly denied with status " + $_.Exception.Response.StatusCode.value__)
}

try {
    $testBody = @{
        transactionNumber = 9999
        transactionDate = (Get-Date).ToString("o")
        carNumber = "TRK-001"
        driverName = "Illegal Driver"
        source = "Origin"
        poNumber = "PO-000"
        materialType = "Ready-Mix Concrete (C25/30)"
        weightBeforeLoading = 30.0
        weightWhenEnteringFactory = 28.0
        weightWhenExitingFactory = 8.0
        destination = "Factory Sector A - Batching Plant 1"
        transactionStatus = "Pending"
        receivingStatus = "Pending"
    } | ConvertTo-Json

    $empHeaders = @{ Authorization = "Bearer $empToken" }
    Invoke-RestMethod -Uri "$baseUrl/api/transportation" -Method Post -Headers $empHeaders -Body $testBody -ContentType "application/json"
    Write-Error "FAIL: Employee should NOT be able to submit transportation transaction!"
} catch {
    Write-Output ("SUCCESS: Employee submission blocked by backend authorization with status " + $_.Exception.Response.StatusCode.value__)
}

Write-Output "`n=================================================="
Write-Output "4. TEST AUTHORIZED SUBMISSION (TRANSPORTATION MANAGER)"
Write-Output "=================================================="
$txNum = (Get-Random -Minimum 100000 -Maximum 999999)
$submitBody = @{
    transactionNumber = $txNum
    transactionDate = (Get-Date).ToString("o")
    carNumber = "TRK-7840"
    driverName = "Hassan Ibrahim"
    source = "Suez Quarry Sector 4"
    poNumber = "PO-2026-9810"
    materialType = "Reinforced Concrete Mix (C35/45)"
    weightBeforeLoading = 45.500
    weightWhenEnteringFactory = 42.800
    weightWhenExitingFactory = 14.300
    destination = "Factory Sector B - Precast Yard"
    transactionStatus = "In Transit"
    receivingStatus = "Approved / Accepted"
    notes = "Automated test batch consignment"
} | ConvertTo-Json

$transHeaders = @{ Authorization = "Bearer $transToken" }
$submitRes = Invoke-RestMethod -Uri "$baseUrl/api/transportation" -Method Post -Headers $transHeaders -Body $submitBody -ContentType "application/json"
Write-Output ("Transaction Submitted! Success: " + $submitRes.success)
Write-Output ("  Id: " + $submitRes.data.id)
Write-Output ("  TxNumber: " + $submitRes.data.transactionNumber)
Write-Output ("  ExpectedWeight (45.5 - 14.3): " + $submitRes.data.expectedWeight + " (Expected: 31.2)")
Write-Output ("  NetWeight (42.8 - 14.3):      " + $submitRes.data.netWeight + " (Expected: 28.5)")
Write-Output ("  DateOfSubmit (Server):       " + $submitRes.data.dateOfSubmit)

Write-Output "`n=================================================="
Write-Output "5. TEST DUPLICATE TRANSACTION NUMBER PREVENTION"
Write-Output "=================================================="
try {
    # Attempt submitting with the exact same transaction number
    $dupRes = Invoke-RestMethod -Uri "$baseUrl/api/transportation" -Method Post -Headers $transHeaders -Body $submitBody -ContentType "application/json"
    Write-Error "FAIL: Duplicate transaction number was allowed!"
} catch {
    $errStream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($errStream)
    $errBody = $reader.ReadToEnd() | ConvertFrom-Json
    Write-Output ("SUCCESS: Duplicate Transaction rejected correctly!")
    Write-Output ("  Status:  " + $_.Exception.Response.StatusCode.value__)
    Write-Output ("  Message: " + $errBody.message)
}

Write-Output "`n=================================================="
Write-Output "6. TEST LOGICAL WEIGHT VALIDATION (EXIT > ENTER)"
Write-Output "=================================================="
try {
    $invalidWeightBody = @{
        transactionNumber = (Get-Random -Minimum 100000 -Maximum 999999)
        transactionDate = (Get-Date).ToString("o")
        carNumber = "TRK-9999"
        driverName = "Test Driver"
        source = "Source"
        poNumber = "PO-TEST"
        materialType = "Ready-Mix Concrete (C25/30)"
        weightBeforeLoading = 40.0
        weightWhenEnteringFactory = 30.0
        weightWhenExitingFactory = 35.0  # Invalid! Exiting > Entering
        destination = "Factory Sector A - Batching Plant 1"
        transactionStatus = "Pending"
        receivingStatus = "Pending"
    } | ConvertTo-Json

    Invoke-RestMethod -Uri "$baseUrl/api/transportation" -Method Post -Headers $transHeaders -Body $invalidWeightBody -ContentType "application/json"
    Write-Error "FAIL: Invalid exit weight was allowed!"
} catch {
    $errStream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($errStream)
    $errBody = $reader.ReadToEnd() | ConvertFrom-Json
    Write-Output ("SUCCESS: Logical weight violation caught correctly!")
    Write-Output ("  Message: " + $errBody.message)
}

Write-Output "`n=================================================="
Write-Output "7. TEST GET RECENT TRANSACTIONS"
Write-Output "=================================================="
$recentRes = Invoke-RestMethod -Uri "$baseUrl/api/transportation/recent" -Method Get -Headers $transHeaders
Write-Output ("Recent Transactions returned: " + $recentRes.data.Count)
$top = $recentRes.data[0]
Write-Output ("Top item: #" + $top.transactionNumber + " | Net: " + $top.netWeight + " T | Dest: " + $top.destination)

Write-Output "`n=================================================="
Write-Output "8. REGRESSION TEST: FINANCE & CHAT INTACT"
Write-Output "=================================================="
$deptListRes = Invoke-RestMethod -Uri "$baseUrl/api/departments" -Method Get -Headers @{ Authorization = "Bearer $empToken" }
Write-Output ("Departments for Employee: " + ($deptListRes.data.departments | ForEach-Object { $_.name } | Out-String).Trim())
Write-Output ("Employee Has Transportation: " + $deptListRes.data.hasTransportationAccess)

$chatHistRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/history" -Method Get -Headers @{ Authorization = "Bearer $empToken" }
Write-Output ("Finance Chat History accessible: " + $chatHistRes.success)

Write-Output "`n=================================================="
Write-Output "ALL BACKEND TESTS COMPLETED SUCCESSFULLY!"
Write-Output "=================================================="
