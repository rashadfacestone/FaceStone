[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls13

$baseUrl = "https://localhost:52563"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " TESTING SIGNALR & API ON HTTPS: $baseUrl" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Test Root
$rootRes = Invoke-WebRequest -Uri "$baseUrl" -UseBasicParsing
Write-Host "[1] GET $baseUrl -> HTTP Status: $($rootRes.StatusCode)" -ForegroundColor Green

# 2. Test User Logins
Write-Host "`n[2] Logging in Alex [Employee] and Sarah [Accountant]..." -ForegroundColor Yellow
$alexBody = @{ identifier = "alex@facestone.com"; password = "Password123!" } | ConvertTo-Json
$alexLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $alexBody -ContentType "application/json"
$alexToken = $alexLogin.data.token
$alexUser = $alexLogin.data.user
Write-Host " -> Alex authenticated (ID: $($alexUser.id), Role: $($alexUser.role))" -ForegroundColor Green

$sarahBody = @{ identifier = "sarah@facestone.com"; password = "SecretPass123!" } | ConvertTo-Json
$sarahLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $sarahBody -ContentType "application/json"
$sarahToken = $sarahLogin.data.token
$sarahUser = $sarahLogin.data.user
Write-Host " -> Sarah authenticated (ID: $($sarahUser.id), Role: $($sarahUser.role))" -ForegroundColor Green

# 3. Test SignalR Negotiate
Write-Host "`n[3] Testing SignalR Negotiation on https://localhost:52563/chatHub/negotiate..." -ForegroundColor Yellow
$negUrl = "$baseUrl/chatHub/negotiate?negotiateVersion=1"
$negHeaders = @{ Authorization = "Bearer $alexToken" }
$negRes = Invoke-RestMethod -Uri $negUrl -Method Post -Headers $negHeaders
Write-Host " -> Negotiation Succeeded!" -ForegroundColor Green
Write-Host "    ConnectionId:      $($negRes.connectionId)" -ForegroundColor Green
Write-Host "    ConnectionToken:   $($negRes.connectionToken)" -ForegroundColor Green
Write-Host "    Transports:        $($negRes.availableTransports | ForEach-Object { $_.transport } | Out-String)" -ForegroundColor Green

# 4. Test Sending Message with Image Attachment to uploads/chat
Write-Host "`n[4] Sending Message with JPEG Attachment to verify uploads/chat storage..." -ForegroundColor Yellow
$testJpgPath = "f:\FaceStoneWebsite\scratch\test_photo.jpg"
$clientMsgId = "cmsg_ssl_" + [Guid]::NewGuid().ToString("N")

$raw = & curl.exe -k -s -X POST "$baseUrl/api/chat/send" `
    -H "Authorization: Bearer $alexToken" `
    -F "Message=Testing SSL attachment upload to uploads/chat" `
    -F "ClientMessageId=$clientMsgId" `
    -F "Attachment=@`"$testJpgPath`";filename=`"WhatsApp Image 2026-09-21 at 12.47.12 PM.jpeg`""

$sendRes = $raw | ConvertFrom-Json
if ($sendRes.success -and $sendRes.data.hasAttachment) {
    $att = $sendRes.data.attachment
    Write-Host " -> Message and Attachment Sent Successfully!" -ForegroundColor Green
    Write-Host "    MessageId:    $($sendRes.data.id)"
    Write-Host "    AttachmentId: $($att.id)"
    Write-Host "    Filename:     $($att.fileName)"
    Write-Host "    StoragePath:  $($att.filePath)"
    Write-Host "    Url:          $($att.url)"
} else {
    Write-Host " -> FAILED: $($sendRes.message)" -ForegroundColor Red
    exit 1
}

# 5. Verify file was saved in f:\FaceStoneWebsite\uploads\chat (OUTSIDE wwwroot)
Write-Host "`n[5] Verifying physical disk persistence outside wwwroot..." -ForegroundColor Yellow
$newUploadFile = "f:\FaceStoneWebsite\uploads\chat\$($att.filePath)"
if (Test-Path $newUploadFile) {
    Write-Host " -> SUCCESS: File persisted in uploads/chat (OUTSIDE wwwroot): $newUploadFile" -ForegroundColor Green
} else {
    Write-Host " -> WARNING: File not found in uploads/chat: $newUploadFile" -ForegroundColor Yellow
}

# 6. Stream Image Attachment via GET /api/chat/attachments/{id}?token=...
Write-Host "`n[6] Testing direct image streaming with query token..." -ForegroundColor Yellow
$imgStreamUrl = "$baseUrl/api/chat/attachments/$($att.id)?token=" + [System.Web.HttpUtility]::UrlEncode($alexToken)
$streamRes = Invoke-WebRequest -Uri $imgStreamUrl -Method Get
Write-Host " -> HTTP Status:  $($streamRes.StatusCode)" -ForegroundColor Green
Write-Host " -> Content-Type: $($streamRes.Headers['Content-Type'])" -ForegroundColor Green

# 7. Download Image Attachment via GET /api/chat/attachments/{id}/download
Write-Host "`n[7] Testing download endpoint..." -ForegroundColor Yellow
$dlUrl = "$baseUrl/api/chat/attachments/$($att.id)/download?token=" + [System.Web.HttpUtility]::UrlEncode($alexToken)
$dlRes = Invoke-WebRequest -Uri $dlUrl -Method Get
Write-Host " -> HTTP Status:         $($dlRes.StatusCode)" -ForegroundColor Green
Write-Host " -> Content-Disposition: $($dlRes.Headers['Content-Disposition'])" -ForegroundColor Green

# 8. Test Accountant Message Reply
Write-Host "`n[8] Sarah [Accountant] replies to Alex..." -ForegroundColor Yellow
$replyClientMsgId = "cmsg_reply_" + [Guid]::NewGuid().ToString("N")
$replyRaw = & curl.exe -k -s -X POST "$baseUrl/api/chat/send" `
    -H "Authorization: Bearer $sarahToken" `
    -F "Message=Approved. Everything looks pristine on site." `
    -F "TargetEmployeeId=$($alexUser.id)" `
    -F "ClientMessageId=$replyClientMsgId"

$replyRes = $replyRaw | ConvertFrom-Json
if ($replyRes.success) {
    Write-Host " -> Accountant reply delivered successfully! (MessageId: $($replyRes.data.id))" -ForegroundColor Green
} else {
    Write-Host " -> FAILED Accountant reply: $($replyRes.message)" -ForegroundColor Red
}

# 9. Verify History & Persistence for both
Write-Host "`n[9] Verifying persistence across queries..." -ForegroundColor Yellow
$alexHistory = Invoke-RestMethod -Uri "$baseUrl/api/chat/history" -Headers @{ Authorization = "Bearer $alexToken" }
$foundMsg = $alexHistory.data | Where-Object { $_.id -eq $sendRes.data.id }
if ($foundMsg -and $foundMsg.attachment -and $foundMsg.attachment.fileName -like "*WhatsApp Image*") {
    Write-Host " -> SUCCESS: Alex history contains the message and image attachment." -ForegroundColor Green
} else {
    Write-Host " -> FAILED to find message in Alex history" -ForegroundColor Red
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host " ALL HTTPS REAL-TIME & ATTACHMENT TESTS COMPLETED!" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
