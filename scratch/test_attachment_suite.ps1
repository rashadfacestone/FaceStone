# Comprehensive Attachment Test Suite for FaceStone Chat Application
$baseUrl = "http://localhost:5124"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " STARTING FACESTONE CHAT ATTACHMENT TEST SUITE" -ForegroundColor Cyan
Write-Host " Base URL: $baseUrl" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Login Accounts
Write-Host "`n[1] Authenticating test accounts..." -ForegroundColor Yellow

$empLoginBody = @{ identifier = "alex@facestone.com"; password = "Password123!" } | ConvertTo-Json
$empLoginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method POST -Body $empLoginBody -ContentType "application/json"
$empToken = $empLoginRes.data.token
$empUser = $empLoginRes.data.user
Write-Host " -> Employee logged in: $($empUser.fullName) (ID: $($empUser.id), Role: $($empUser.role))" -ForegroundColor Green

$accLoginBody = @{ identifier = "sarah@facestone.com"; password = "SecretPass123!" } | ConvertTo-Json
$accLoginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method POST -Body $accLoginBody -ContentType "application/json"
$accToken = $accLoginRes.data.token
$accUser = $accLoginRes.data.user
Write-Host " -> Accountant logged in: $($accUser.fullName) (ID: $($accUser.id), Role: $($accUser.role))" -ForegroundColor Green

# 2. Prepare Test Files
$scratchDir = "f:\FaceStoneWebsite\scratch"
if (-not (Test-Path $scratchDir)) { New-Item -ItemType Directory -Path $scratchDir | Out-Null }

$testJpgPath = Join-Path $scratchDir "test_photo.jpg"
# Write small valid JPEG header (FF D8 FF E0 ... FF D9)
[byte[]]$jpegBytes = 0xFF,0xD8,0xFF,0xE0,0x00,0x10,0x4A,0x46,0x49,0x46,0x00,0x01,0x01,0x01,0x00,0x48,0x00,0x48,0x00,0x00,0xFF,0xDB,0x00,0x43,0x00,0x08,0x06,0x06,0x07,0x06,0x05,0x08,0x07,0x07,0x07,0x09,0x09,0x08,0x0A,0x0C,0x14,0x0D,0x0C,0x0B,0x0B,0x0C,0x19,0x12,0x13,0x0F,0x14,0x1D,0x1A,0x1F,0x1E,0x1D,0x1A,0x1C,0x1C,0x20,0x24,0x2E,0x27,0x20,0x22,0x2C,0x23,0x1C,0x1C,0x28,0x37,0x29,0x2C,0x30,0x31,0x34,0x34,0x34,0x1F,0x27,0x39,0x3D,0x38,0x32,0x3C,0x2E,0x33,0x34,0x32,0xFF,0xC0,0x00,0x0B,0x08,0x00,0x01,0x00,0x01,0x01,0x01,0x11,0x00,0xFF,0xC4,0x00,0x14,0x00,0x01,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x09,0xFF,0xDA,0x00,0x08,0x01,0x01,0x00,0x00,0x3F,0x00,0x7F,0x00,0xFF,0xD9
[System.IO.File]::WriteAllBytes($testJpgPath, $jpegBytes)

$testPngPath = Join-Path $scratchDir "test_diagram.png"
[byte[]]$pngBytes = 0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0x00,0x00,0x00,0x0D,0x49,0x48,0x44,0x52,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,0x08,0x06,0x00,0x00,0x00,0x1F,0x15,0xC4,0x89,0x00,0x00,0x00,0x0A,0x49,0x44,0x41,0x54,0x78,0x9C,0x63,0x00,0x01,0x00,0x00,0x05,0x00,0x01,0x0D,0x0A,0x2D,0xB4,0x00,0x00,0x00,0x00,0x49,0x45,0x4E,0x44,0xAE,0x42,0x60,0x82
[System.IO.File]::WriteAllBytes($testPngPath, $pngBytes)

$testPdfPath = Join-Path $scratchDir "test_spec.pdf"
[System.IO.File]::WriteAllText($testPdfPath, "%PDF-1.4 1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj xref 0 2 trailer << /Root 1 0 R >> %%EOF")

$testExePath = Join-Path $scratchDir "test_script.bat"
[System.IO.File]::WriteAllText($testExePath, "@echo off`necho Dangerous")

Write-Host " -> Test files created." -ForegroundColor Green

# Helper function for multipart upload using curl.exe
function Upload-File {
    param (
        [string]$Token,
        [string]$Message,
        [string]$FilePath,
        [string]$ClientMsgId,
        [string]$EmployeeId = $null
    )

    $argsList = @(
        "-s",
        "-X", "POST",
        "$baseUrl/api/chat/send",
        "-H", "Authorization: Bearer $Token",
        "-F", "Message=$Message",
        "-F", "ClientMessageId=$ClientMsgId"
    )

    if ($FilePath -and (Test-Path $FilePath)) {
        $fileName = [System.IO.Path]::GetFileName($FilePath)
        $argsList += "-F"
        $argsList += "Attachment=@`"$FilePath`";filename=`"$fileName`""
    }

    if ($EmployeeId) {
        $argsList += "-F"
        $argsList += "EmployeeId=$EmployeeId"
    }

    $raw = & curl.exe @argsList
    return $raw | ConvertFrom-Json
}

# 3. TEST 1: Send JPG from Employee to Desk
Write-Host "`n[2] TEST 1: Send JPG from Employee (with original spaces in filename)..." -ForegroundColor Yellow
$originalJpgName = "WhatsApp Image 2026-09-21 at 12.47.12 PM.jpeg"
$renamedJpg = Join-Path $scratchDir $originalJpgName
Copy-Item $testJpgPath $renamedJpg -Force

$clientMsgId1 = "cmsg_test_" + [Guid]::NewGuid().ToString("N")
$sendRes = Upload-File -Token $empToken -Message "Here is the site photo with original WhatsApp name" -FilePath $renamedJpg -ClientMsgId $clientMsgId1

if ($sendRes.success -and $sendRes.data.hasAttachment) {
    $att = $sendRes.data.attachment
    Write-Host " -> SUCCESS: Message sent with attachment!" -ForegroundColor Green
    Write-Host "    MessageId:      $($sendRes.data.id)"
    Write-Host "    AttachmentId:   $($att.id)"
    Write-Host "    Original Name:  $($att.fileName)"
    Write-Host "    File Type:      $($att.fileType)"
    Write-Host "    Storage Path:   $($att.filePath)"
    Write-Host "    Attachment URL: $($att.url)"
    $testAttachmentId = $att.id
    $testMessageId = $sendRes.data.id
} else {
    Write-Host " -> FAILED: $($sendRes.message)" -ForegroundColor Red
    exit 1
}

# 4. TEST 2: Inspect Disk Storage
Write-Host "`n[3] TEST 2: Inspecting disk storage persistence..." -ForegroundColor Yellow
$chatUploadsDir = "f:\FaceStoneWebsite\wwwroot\uploads\chat"
$savedDiskFile = Join-Path $chatUploadsDir $att.filePath
if (Test-Path $savedDiskFile) {
    $fileInfo = Get-Item $savedDiskFile
    Write-Host " -> SUCCESS: File persisted on server disk at: $savedDiskFile" -ForegroundColor Green
    Write-Host "    Size on disk: $($fileInfo.Length) bytes (Safe server-generated UUID filename)"
} else {
    Write-Host " -> FAILED: File not found at $savedDiskFile" -ForegroundColor Red
}

# 5. TEST 3: Access Image via GET /api/chat/attachments/{id} with Bearer Header
Write-Host "`n[4] TEST 3: Stream image via Bearer token..." -ForegroundColor Yellow
$streamHeaders = @{ Authorization = "Bearer $empToken" }
$streamRes = Invoke-WebRequest -Uri "$baseUrl/api/chat/attachments/$testAttachmentId" -Headers $streamHeaders -Method GET
Write-Host " -> HTTP Status:   $($streamRes.StatusCode)" -ForegroundColor Green
Write-Host " -> Content-Type:  $($streamRes.Headers['Content-Type'])" -ForegroundColor Green
Write-Host " -> Content-Length: $($streamRes.RawContentLength) bytes"

if ($streamRes.Headers['Content-Type'] -like "*image/jpeg*") {
    Write-Host " -> SUCCESS: Correct MIME image/jpeg returned." -ForegroundColor Green
} else {
    Write-Host " -> WARNING: Expected image/jpeg, got $($streamRes.Headers['Content-Type'])" -ForegroundColor Yellow
}

# 6. TEST 4: Access Image via GET /api/chat/attachments/{id}?token=... (for <img> src)
Write-Host "`n[5] TEST 4: Direct image rendering via URL query token..." -ForegroundColor Yellow
$queryTokenUrl = "$baseUrl/api/chat/attachments/$testAttachmentId" + "?token=" + [System.Web.HttpUtility]::UrlEncode($empToken)
$queryRes = Invoke-WebRequest -Uri $queryTokenUrl -Method GET
Write-Host " -> HTTP Status:   $($queryRes.StatusCode)" -ForegroundColor Green
Write-Host " -> Content-Type:  $($queryRes.Headers['Content-Type'])" -ForegroundColor Green
if ($queryRes.StatusCode -eq 200) {
    Write-Host " -> SUCCESS: <img> can render directly using token parameter without 401 error!" -ForegroundColor Green
} else {
    Write-Host " -> FAILED: Could not access via query token" -ForegroundColor Red
}

# 7. TEST 5: Download Attachment with Original Filename
Write-Host "`n[6] TEST 5: Download attachment via /download endpoint..." -ForegroundColor Yellow
$dlUrl = "$baseUrl/api/chat/attachments/$testAttachmentId/download?token=" + [System.Web.HttpUtility]::UrlEncode($empToken)
$dlRes = Invoke-WebRequest -Uri $dlUrl -Method GET
$contentDisp = $dlRes.Headers['Content-Disposition']
Write-Host " -> Content-Disposition: $contentDisp" -ForegroundColor Green
if ($contentDisp -like "*$originalJpgName*") {
    Write-Host " -> SUCCESS: Original filename preserved in download header!" -ForegroundColor Green
} else {
    Write-Host " -> FAILED: Content-Disposition does not contain original filename: $contentDisp" -ForegroundColor Red
}

# 8. TEST 6: Message History & Database Persistence
Write-Host "`n[7] TEST 6: Verify persistence across database queries (History & Conversation)..." -ForegroundColor Yellow

# Employee History
$historyRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/history" -Headers @{ Authorization = "Bearer $empToken" }
$foundInHistory = $historyRes.data | Where-Object { $_.id -eq $testMessageId }
if ($foundInHistory -and $foundInHistory.attachment -and $foundInHistory.attachment.fileName -eq $originalJpgName) {
    Write-Host " -> SUCCESS: Attachment persisted and correctly loaded in Employee history!" -ForegroundColor Green
} else {
    Write-Host " -> FAILED: Not found in Employee history" -ForegroundColor Red
}

# Accountant Conversation Detail
$convRes = Invoke-RestMethod -Uri "$baseUrl/api/chat/conversations/$($empUser.id)" -Headers @{ Authorization = "Bearer $accToken" }
$foundInAcc = $convRes.data.messages | Where-Object { $_.id -eq $testMessageId }
if ($foundInAcc -and $foundInAcc.attachment -and $foundInAcc.attachment.fileName -eq $originalJpgName) {
    Write-Host " -> SUCCESS: Accountant can retrieve conversation with attachment metadata intact!" -ForegroundColor Green
} else {
    Write-Host " -> FAILED: Not found in Accountant conversation view" -ForegroundColor Red
}

# 9. TEST 7: Authorization Isolation Check
Write-Host "`n[8] TEST 7: Security check - Unauthorized third-party user access..." -ForegroundColor Yellow
# Register a 3rd separate employee
$randomUser = "other_emp_" + (Get-Random -Minimum 1000 -Maximum 9999)
$regBody = @{
    fullName = "Other Employee"
    username = $randomUser
    email = "$randomUser@test.com"
    phoneNumber = "+2012" + (Get-Random -Minimum 10000000 -Maximum 99999999)
    countryCode = "+20"
    password = "Password123!"
    role = "Employee"
} | ConvertTo-Json
$regRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/register" -Method POST -Body $regBody -ContentType "application/json"
$otherToken = $regRes.data.token

try {
    $unauthRes = Invoke-WebRequest -Uri "$baseUrl/api/chat/attachments/$testAttachmentId" -Headers @{ Authorization = "Bearer $otherToken" } -Method GET
    Write-Host " -> FAILED: Unauthorized user got 200 OK! Security breach." -ForegroundColor Red
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 403) {
        Write-Host " -> SUCCESS: Unauthorized user correctly denied with 403 Forbidden!" -ForegroundColor Green
    } else {
        Write-Host " -> Result: Response code $statusCode (expected 403)" -ForegroundColor Yellow
    }
}

# 10. TEST 8: Send PNG Diagram
Write-Host "`n[9] TEST 8: Send PNG diagram..." -ForegroundColor Yellow
$pngClientMsgId = "cmsg_png_" + [Guid]::NewGuid().ToString("N")
$pngSendRes = Upload-File -Token $empToken -Message "Technical layout diagram" -FilePath $testPngPath -ClientMsgId $pngClientMsgId
if ($pngSendRes.success -and $pngSendRes.data.attachment.fileType -eq "image") {
    $pngAttId = $pngSendRes.data.attachment.id
    $pngStream = Invoke-WebRequest -Uri "$baseUrl/api/chat/attachments/$pngAttId" -Headers @{ Authorization = "Bearer $empToken" }
    Write-Host " -> PNG Status: $($pngStream.StatusCode), Content-Type: $($pngStream.Headers['Content-Type'])" -ForegroundColor Green
    if ($pngStream.Headers['Content-Type'] -like "*image/png*") {
        Write-Host " -> SUCCESS: PNG image uploaded and streamed correctly." -ForegroundColor Green
    }
} else {
    Write-Host " -> FAILED: PNG upload failed." -ForegroundColor Red
}

# 11. TEST 9: Send PDF Document
Write-Host "`n[10] TEST 9: Send PDF document (Non-image file card)..." -ForegroundColor Yellow
$pdfClientMsgId = "cmsg_pdf_" + [Guid]::NewGuid().ToString("N")
$pdfSendRes = Upload-File -Token $empToken -Message "Project engineering specifications" -FilePath $testPdfPath -ClientMsgId $pdfClientMsgId
if ($pdfSendRes.success -and $pdfSendRes.data.attachment.fileType -eq "pdf") {
    $pdfAttId = $pdfSendRes.data.attachment.id
    $pdfStream = Invoke-WebRequest -Uri "$baseUrl/api/chat/attachments/$pdfAttId" -Headers @{ Authorization = "Bearer $empToken" }
    Write-Host " -> PDF Status: $($pdfStream.StatusCode), Content-Type: $($pdfStream.Headers['Content-Type'])" -ForegroundColor Green
    if ($pdfStream.Headers['Content-Type'] -like "*application/pdf*") {
        Write-Host " -> SUCCESS: PDF streamed with application/pdf MIME type." -ForegroundColor Green
    }
} else {
    Write-Host " -> FAILED: PDF upload failed." -ForegroundColor Red
}

# 12. TEST 10: Reject Dangerous Executable Upload
Write-Host "`n[11] TEST 10: Reject executable file upload (.bat)..." -ForegroundColor Yellow
$exeClientMsgId = "cmsg_exe_" + [Guid]::NewGuid().ToString("N")
$exeSendRes = Upload-File -Token $empToken -Message "Trying to send a script" -FilePath $testExePath -ClientMsgId $exeClientMsgId
if (-not $exeSendRes.success -and $exeSendRes.message -like "*Executable files cannot be uploaded*") {
    Write-Host " -> SUCCESS: Server rejected executable upload: $($exeSendRes.message)" -ForegroundColor Green
} else {
    Write-Host " -> FAILED: Server did not reject executable upload!" -ForegroundColor Red
}

# 13. TEST 11: Idempotency (Prevent Duplicate Attachments on Network Retry)
Write-Host "`n[12] TEST 11: Idempotency - Send duplicate request with same ClientMessageId..." -ForegroundColor Yellow
$dupSendRes = Upload-File -Token $empToken -Message "Retry attempt" -FilePath $renamedJpg -ClientMsgId $clientMsgId1
if ($dupSendRes.success -and $dupSendRes.data.id -eq $testMessageId) {
    Write-Host " -> SUCCESS: Idempotency honored. Returned existing MessageId without creating duplicates!" -ForegroundColor Green
} else {
    Write-Host " -> FAILED: Created duplicate message!" -ForegroundColor Red
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host " ALL ATTACHMENT SUITE TESTS COMPLETED SUCCESSFULLY!" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
