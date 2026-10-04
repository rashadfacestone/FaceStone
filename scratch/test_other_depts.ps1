$baseUrl = "http://localhost:5124"

# Test 14: Finance
$accBody = @{ identifier = "sarah@facestone.com"; password = "Password123!" } | ConvertTo-Json
$accLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $accBody -ContentType "application/json"
$accHeaders = @{ "Authorization" = "Bearer $($accLogin.data.token)" }

$mgmtDataRes = Invoke-RestMethod -Uri "$baseUrl/api/finance/management-data" -Method Get -Headers $accHeaders
$finPoResponse = Invoke-WebRequest -Uri "$baseUrl/api/finance/po/PO-2026-9810" -Headers $accHeaders
$finPoIsFallbackHtml = ($finPoResponse.Headers["Content-Type"].Contains("text/html"))
$p14 = ($mgmtDataRes.success -and $mgmtDataRes.data.records.Count -gt 0 -and $finPoIsFallbackHtml)
Write-Host "Test 14 (Finance Unmodified): Passed=$p14, PO records count=$($mgmtDataRes.data.records.Count), Old Finance PO endpoint is removed (served fallback HTML: $finPoIsFallbackHtml)"

# Test 15: Storage
$storageBody = @{ identifier = "storage@facestone.com"; password = "Storage123!" } | ConvertTo-Json
$storageLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $storageBody -ContentType "application/json"
$storageHeaders = @{ "Authorization" = "Bearer $($storageLogin.data.token)" }
$storageConfig = Invoke-RestMethod -Uri "$baseUrl/api/storage/config" -Method Get -Headers $storageHeaders
$p15 = ($storageConfig.success -and $storageConfig.data.storageLocations.Count -gt 0)
Write-Host "Test 15 (Storage Unmodified): Passed=$p15, Locations count=$($storageConfig.data.storageLocations.Count)"

# Test 16: Management
$ceoBody = @{ identifier = "kareem.ceo@facestone.com"; password = "Password123!" } | ConvertTo-Json
$ceoLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $ceoBody -ContentType "application/json"
$ceoHeaders = @{ "Authorization" = "Bearer $($ceoLogin.data.token)" }
$mgmtRes = Invoke-RestMethod -Uri "$baseUrl/api/management/data" -Method Get -Headers $ceoHeaders
$p16 = ($mgmtRes.success -and $mgmtRes.data.records.Count -ge 0)
Write-Host "Test 16 (Management Unmodified): Passed=$p16, Records count=$($mgmtRes.data.records.Count)"
