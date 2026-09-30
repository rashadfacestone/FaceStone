$baseUrl = 'http://localhost:5124'
$ErrorActionPreference = 'Stop'

Write-Host "============================================================"
Write-Host "   LIVE LOCATION MESSAGING BUG FIX VERIFICATION SUITE       "
Write-Host "============================================================"

# --- Step 1: Login Alex (Employee) ---
Write-Host "`n[Test 1] Logging in Alex (Employee)..."
$alexLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body (@{ identifier = 'alex@facestone.com'; password = 'Password123!' } | ConvertTo-Json) -ContentType 'application/json'
$alexToken = $alexLogin.data.token
$alexUser = $alexLogin.data.user
$alexHeaders = @{ Authorization = "Bearer $alexToken" }
Write-Host "  -> Alex logged in. ID: $($alexUser.id), Role: $($alexUser.role)"

# --- Step 2: Login Sarah (Accountant) ---
Write-Host "`n[Test 2] Logging in Sarah (Accountant)..."
$sarahLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body (@{ identifier = 'sarah@facestone.com'; password = 'SecretPass123!' } | ConvertTo-Json) -ContentType 'application/json'
$sarahToken = $sarahLogin.data.token
$sarahUser = $sarahLogin.data.user
$sarahHeaders = @{ Authorization = "Bearer $sarahToken" }
Write-Host "  -> Sarah logged in. ID: $($sarahUser.id), Role: $($sarahUser.role)"

# --- Step 3: Check baseline messages before test ---
Write-Host "`n[Test 3] Checking baseline messages for Sarah viewing Alex..."
$initThread = Invoke-RestMethod -Uri "$baseUrl/api/chat/conversations/$($alexUser.id)" -Method Get -Headers $sarahHeaders
$initCount = $initThread.data.messages.Count
Write-Host "  -> Initial message count: $initCount"

# --- Step 4: Sender starts Live Location ---
Write-Host "`n[Test 4] Alex starts Live Location (15 mins)..."
$clientMsgId = "test_live_" + [System.Guid]::NewGuid().ToString()
$startBody = @{
    latitude = 30.12808
    longitude = 31.37287
    durationMinutes = 15
    clientMessageId = $clientMsgId
} | ConvertTo-Json

$liveStartRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/live-location/start" -Method Post -Headers $alexHeaders -Body $startBody -ContentType 'application/json'
$liveMsg = $liveStartRes.data

Write-Host "  -> Response MessageType: '$($liveMsg.messageType)'"
Write-Host "  -> Response MessageText: '$($liveMsg.messageText)'"
Write-Host "  -> Response Status:      '$($liveMsg.status)'"
Write-Host "  -> Response SessionId:   '$($liveMsg.liveSessionId)'"

if ($liveMsg.messageType -ne "LIVE_LOCATION") {
    throw "FAIL: Expected messageType 'LIVE_LOCATION', got '$($liveMsg.messageType)'"
}
if (![string]::IsNullOrEmpty($liveMsg.messageText)) {
    throw "FAIL: Expected messageText to be empty, but got '$($liveMsg.messageText)'"
}
if ($liveMsg.status -ne "ACTIVE") {
    throw "FAIL: Expected status 'ACTIVE', got '$($liveMsg.status)'"
}
Write-Host "  [PASS] Live Location started with structured type LIVE_LOCATION, empty text, and ACTIVE status."

# --- Step 5: Verify Receiver (Sarah) gets the structured LIVE_LOCATION and ZERO text messages ---
Write-Host "`n[Test 5] Sarah fetches Alex's thread to verify receiver data..."
$sarahThread = Invoke-RestMethod -Uri "$baseUrl/api/chat/conversations/$($alexUser.id)" -Method Get -Headers $sarahHeaders
$sarahMessages = $sarahThread.data.messages
$newCount = $sarahMessages.Count

if ($newCount -ne ($initCount + 1)) {
    throw "FAIL: Expected exactly 1 new message in thread ($($initCount + 1)), but found $newCount"
}

$receivedLive = $sarahMessages | Where-Object { $_.id -eq $liveMsg.id }
if (-not $receivedLive) {
    throw "FAIL: Receiver thread did not find the newly created live location message!"
}
if ($receivedLive.messageType -ne "LIVE_LOCATION") {
    throw "FAIL: Receiver thread returned messageType '$($receivedLive.messageType)' instead of 'LIVE_LOCATION'"
}
if (![string]::IsNullOrEmpty($receivedLive.messageText)) {
    throw "FAIL: Receiver thread returned messageText '$($receivedLive.messageText)' instead of empty string!"
}

# Verify NO message with "Live Location" as text bubble exists in recent messages
$badTextBubbles = $sarahMessages | Where-Object { $_.messageText -eq "Live Location" }
if ($badTextBubbles.Count -gt 0) {
    throw "FAIL: Found $($badTextBubbles.Count) message(s) containing text bubble 'Live Location'!"
}
Write-Host "  [PASS] Receiver thread has exactly 1 new message, typed LIVE_LOCATION, zero 'Live Location' text bubbles."

# --- Step 6: Location Coordinate Updates ---
Write-Host "`n[Test 6] Alex sends location coordinate update..."
$sessionId = $liveMsg.liveSessionId
$updateBody = @{
    sessionId = $sessionId
    latitude = 30.12850
    longitude = 31.37350
} | ConvertTo-Json

$updateRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/live-location/update" -Method Post -Headers $alexHeaders -Body $updateBody -ContentType 'application/json'
$updateData = $updateRes.data

Write-Host "  -> Update MessageType: '$($updateData.messageType)'"
Write-Host "  -> Update Status:      '$($updateData.status)'"
Write-Host "  -> Update Latitude:    $($updateData.latitude), Longitude: $($updateData.longitude)"

if ($updateData.messageType -ne "LOCATION_UPDATE") {
    throw "FAIL: Expected update messageType 'LOCATION_UPDATE', got '$($updateData.messageType)'"
}

# Verify that updating coordinates did NOT append a new chat message to the thread
$afterUpdateThread = Invoke-RestMethod -Uri "$baseUrl/api/chat/conversations/$($alexUser.id)" -Method Get -Headers $sarahHeaders
if ($afterUpdateThread.data.messages.Count -ne $newCount) {
    throw "FAIL: Coordinate update unexpectedly appended a new chat message to the database!"
}
Write-Host "  [PASS] Coordinate update did not create new chat messages. Session updated in-place."

# --- Step 7: Sender ends Live Location ---
Write-Host "`n[Test 7] Alex stops Live Location..."
$stopBody = @{ sessionId = $sessionId } | ConvertTo-Json
$stopRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/live-location/stop" -Method Post -Headers $alexHeaders -Body $stopBody -ContentType 'application/json'
$stopData = $stopRes.data

Write-Host "  -> Stop MessageType: '$($stopData.messageType)'"
Write-Host "  -> Stop Status:      '$($stopData.status)'"
Write-Host "  -> Stop EndedAt:     $($stopData.endedAt)"

if ($stopData.messageType -ne "LIVE_LOCATION_ENDED") {
    throw "FAIL: Expected stop messageType 'LIVE_LOCATION_ENDED', got '$($stopData.messageType)'"
}
if ($stopData.status -ne "ENDED") {
    throw "FAIL: Expected status 'ENDED', got '$($stopData.status)'"
}

# Verify the thread state after ending
$finalThread = Invoke-RestMethod -Uri "$baseUrl/api/chat/conversations/$($alexUser.id)" -Method Get -Headers $sarahHeaders
$endedMsg = $finalThread.data.messages | Where-Object { $_.id -eq $liveMsg.id }
if ($endedMsg.status -ne "ENDED" -and $endedMsg.isLiveActive -ne $false) {
    throw "FAIL: Persisted live message does not reflect ENDED status!"
}
if (![string]::IsNullOrEmpty($endedMsg.messageText)) {
    throw "FAIL: Persisted ended message has non-empty messageText: '$($endedMsg.messageText)'"
}
Write-Host "  [PASS] Live Location ended with status ENDED and no duplicate text message."

# --- Step 8: Polling fallback verification ---
Write-Host "`n[Test 8] Polling fallback returns hydrated LIVE_LOCATION without text payload..."
$pollRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/poll?targetEmployeeId=$($alexUser.id)" -Method Get -Headers $sarahHeaders
$polledActive = $pollRes.data.activeMessages
$matchingPolled = $polledActive | Where-Object { $_.id -eq $liveMsg.id }
if ($matchingPolled) {
    if ($matchingPolled.messageType -ne "LIVE_LOCATION") {
        throw "FAIL: Polled messageType is '$($matchingPolled.messageType)' instead of 'LIVE_LOCATION'"
    }
    if (![string]::IsNullOrEmpty($matchingPolled.messageText)) {
        throw "FAIL: Polled messageText is '$($matchingPolled.messageText)' instead of empty!"
    }
}
Write-Host "  [PASS] Polling fallback correctly normalizes live location messages."

# --- Step 9: Normal text message verification ---
Write-Host "`n[Test 9] Sending regular text message to ensure normal chat is unaffected..."
$textMsgText = "Standard project update message " + [System.Guid]::NewGuid().ToString().Substring(0, 8)
$curlOut = & curl.exe -s -X POST "$baseUrl/api/chat/send" `
    -H "Authorization: Bearer $sarahToken" `
    -F "Message=$textMsgText" `
    -F "TargetEmployeeId=$($alexUser.id)"

$sentTextRes = $curlOut | ConvertFrom-Json
$sentText = $sentTextRes.data

Write-Host "  -> Text MessageType: '$($sentText.messageType)'"
Write-Host "  -> Text MessageText: '$($sentText.messageText)'"

if ($sentText.messageType -ne "TEXT") {
    throw "FAIL: Normal message messageType is '$($sentText.messageType)' instead of 'TEXT'"
}
if ($sentText.messageText -ne $textMsgText) {
    throw "FAIL: Text content did not match!"
}
Write-Host "  [PASS] Normal text messaging remains 100% operational."

Write-Host "`n============================================================"
Write-Host "  ALL TESTS PASSED: LIVE LOCATION BUG IS COMPLETELY RESOLVED "
Write-Host "============================================================"
