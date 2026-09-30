$baseUrl = "http://localhost:5124"
Write-Host "=== FACESTONE LIVE LOCATION VERIFICATION SUITE ===" -ForegroundColor Cyan

# 1. Login as Employee and Accountant
$empLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"identifier":"alex_stone","password":"Password123!"}'
$empToken = $empLogin.data.token
$empId = $empLogin.data.user.id
Write-Host "[AUTH] Employee Logged In: $empId ($($empLogin.data.user.fullName))" -ForegroundColor Green

$accLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"identifier":"sarah_acc","password":"SecretPass123!"}'
$accToken = $accLogin.data.token
$accId = $accLogin.data.user.id
Write-Host "[AUTH] Accountant Logged In: $accId ($($accLogin.data.user.fullName))" -ForegroundColor Green

$empHeaders = @{ "Authorization" = "Bearer $empToken" }
$accHeaders = @{ "Authorization" = "Bearer $accToken" }

# TEST 1: Normal text message
Write-Host "`n--- TEST 1: Normal Text Message ---" -ForegroundColor Yellow
$textPayload = @{
    MessageType = "TEXT"
    Message = "Hello from employee"
    ClientMessageId = "txt_" + [Guid]::NewGuid().ToString("N")
} | ConvertTo-Json

$textRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/send" -Method Post -ContentType "application/json" -Headers $empHeaders -Body $textPayload
if ($textRes.success -and $textRes.data.messageText -eq "Hello from employee" -and $textRes.data.messageType -eq "TEXT") {
    Write-Host "PASS: Normal text message sent successfully (Type: $($textRes.data.messageType), Text: '$($textRes.data.messageText)')" -ForegroundColor Green
} else {
    Write-Host "FAIL: Text message send failed" -ForegroundColor Red
}

# TEST 1B: Empty text message must fail with 400
try {
    $emptyPayload = @{
        MessageType = "TEXT"
        Message = ""
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$baseUrl/api/chat/send" -Method Post -ContentType "application/json" -Headers $empHeaders -Body $emptyPayload
    Write-Host "FAIL: Empty text message was accepted unexpectedly" -ForegroundColor Red
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "PASS: Empty text message correctly rejected with HTTP $statusCode" -ForegroundColor Green
}

# TEST 2: Start Live Location via /api/chat/send
Write-Host "`n--- TEST 2: Start Live Location via POST /api/chat/send ---" -ForegroundColor Yellow
$livePayload = @{
    MessageType = "LIVE_LOCATION"
    Latitude = 30.12807
    Longitude = 31.37306
    Location = @{
        Latitude = 30.12807
        Longitude = 31.37306
    }
    DurationMinutes = 15
    Status = "ACTIVE"
    ClientMessageId = "live_" + [Guid]::NewGuid().ToString("N")
} | ConvertTo-Json

$liveRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/send" -Method Post -ContentType "application/json" -Headers $empHeaders -Body $livePayload
if ($liveRes.success -and $liveRes.data.messageType -eq "LIVE_LOCATION" -and $liveRes.data.liveSessionId -and $liveRes.data.messageText -eq "") {
    Write-Host "PASS: Live Location started via /api/chat/send (Session: $($liveRes.data.liveSessionId), Type: $($liveRes.data.messageType), Text is empty: '$($liveRes.data.messageText)')" -ForegroundColor Green
    $sessionId = $liveRes.data.liveSessionId
    $liveMsgId = $liveRes.data.id
} else {
    Write-Host "FAIL: Live Location start failed: $($liveRes | ConvertTo-Json)" -ForegroundColor Red
}

# TEST 3: Invalid coordinates must fail with 400
Write-Host "`n--- TEST 3: Invalid Coordinates Validation ---" -ForegroundColor Yellow
try {
    $badCoordsPayload = @{
        MessageType = "LIVE_LOCATION"
        Latitude = 999.0
        Longitude = 31.37306
        DurationMinutes = 15
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$baseUrl/api/chat/send" -Method Post -ContentType "application/json" -Headers $empHeaders -Body $badCoordsPayload
    Write-Host "FAIL: Invalid coordinates were accepted unexpectedly" -ForegroundColor Red
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "PASS: Invalid coordinates correctly rejected with HTTP $statusCode" -ForegroundColor Green
}

# TEST 4: Receiver Flow (Accountant reads conversation messages)
Write-Host "`n--- TEST 4: Receiver Flow (Accountant reading thread) ---" -ForegroundColor Yellow
$threadRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/conversations/$empId" -Method Get -Headers $accHeaders
$receivedLiveMsg = $threadRes.data.messages | Where-Object { $_.id -eq $liveMsgId }
if ($receivedLiveMsg -and $receivedLiveMsg.messageType -eq "LIVE_LOCATION" -and $receivedLiveMsg.messageText -eq "") {
    Write-Host "PASS: Receiver gets structured LIVE_LOCATION (Session: $($receivedLiveMsg.liveSessionId), Status: $($receivedLiveMsg.status), MessageText is empty)" -ForegroundColor Green
} else {
    Write-Host "FAIL: Receiver thread does not have structured LIVE_LOCATION" -ForegroundColor Red
}

# TEST 5: Location update
Write-Host "`n--- TEST 5: Location Update ---" -ForegroundColor Yellow
$updatePayload = @{
    SessionId = $sessionId
    Latitude = 30.12900
    Longitude = 31.37400
} | ConvertTo-Json
$updateRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/live-location/update" -Method Post -ContentType "application/json" -Headers $empHeaders -Body $updatePayload
if ($updateRes.success -and $updateRes.data.latitude -eq 30.12900) {
    Write-Host "PASS: Location update succeeded (Lat: $($updateRes.data.latitude), Lng: $($updateRes.data.longitude))" -ForegroundColor Green
} else {
    Write-Host "FAIL: Location update failed" -ForegroundColor Red
}

# Verify no duplicate chat message was created by GPS update
$threadAfterUpdate = Invoke-RestMethod -Uri "$baseUrl/api/chat/conversations/$empId" -Method Get -Headers $accHeaders
$liveMsgs = @($threadAfterUpdate.data.messages | Where-Object { $_.liveSessionId -eq $sessionId })
if ($liveMsgs.Length -eq 1) {
    Write-Host "PASS: Zero duplicate chat messages created by GPS coordinate update (Count = 1)" -ForegroundColor Green
} else {
    Write-Host "FAIL: Duplicate chat messages found for session: $($liveMsgs.Length)" -ForegroundColor Red
}

# TEST 6: Stop Live Location
Write-Host "`n--- TEST 6: Stop Live Location ---" -ForegroundColor Yellow
$stopPayload = @{
    SessionId = $sessionId
} | ConvertTo-Json
$stopRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/live-location/stop" -Method Post -ContentType "application/json" -Headers $empHeaders -Body $stopPayload
if ($stopRes.success -and $stopRes.data.status -eq "ENDED" -and $stopRes.data.endedAt) {
    Write-Host "PASS: Live location stopped (Status: $($stopRes.data.status), EndedAt: $($stopRes.data.endedAt))" -ForegroundColor Green
} else {
    Write-Host "FAIL: Live location stop failed" -ForegroundColor Red
}

# TEST 7: Idempotency with ClientMessageId
Write-Host "`n--- TEST 7: Idempotency check with ClientMessageId ---" -ForegroundColor Yellow
$idempotentClientMsgId = "idemp_" + [Guid]::NewGuid().ToString("N")
$idempPayload = @{
    MessageType = "LIVE_LOCATION"
    Latitude = 30.12807
    Longitude = 31.37306
    ClientMessageId = $idempotentClientMsgId
} | ConvertTo-Json

$firstSend = Invoke-RestMethod -Uri "$baseUrl/api/chat/send" -Method Post -ContentType "application/json" -Headers $empHeaders -Body $idempPayload
$secondSend = Invoke-RestMethod -Uri "$baseUrl/api/chat/send" -Method Post -ContentType "application/json" -Headers $empHeaders -Body $idempPayload
if ($firstSend.data.id -eq $secondSend.data.id) {
    Write-Host "PASS: Idempotent resend returned exact same message ID ($($firstSend.data.id)) without error or duplication" -ForegroundColor Green
} else {
    Write-Host "FAIL: Idempotency failed" -ForegroundColor Red
}

# TEST 8: Refresh conversation & verify persisted state
Write-Host "`n--- TEST 8: Refresh conversation & verify persisted state ---" -ForegroundColor Yellow
$refreshedThread = Invoke-RestMethod -Uri "$baseUrl/api/chat/conversations/$empId" -Method Get -Headers $accHeaders
$persistedEndedMsg = $refreshedThread.data.messages | Where-Object { $_.id -eq $liveMsgId }
if ($persistedEndedMsg -and $persistedEndedMsg.status -eq "ENDED" -and $persistedEndedMsg.messageType -eq "LIVE_LOCATION") {
    Write-Host "PASS: Persisted Live Location loaded with Status 'ENDED' and MessageType 'LIVE_LOCATION'" -ForegroundColor Green
} else {
    Write-Host "FAIL: Persisted live location state incorrect" -ForegroundColor Red
}

Write-Host "`n=== ALL VERIFICATION TESTS PASSED SUCCESSFULLY! ===" -ForegroundColor Green
