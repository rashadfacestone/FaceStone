# Automated Test Suite for 18 Transportation Department Requirements
$baseUrl = "http://localhost:5124"
$results = [System.Collections.Generic.List[PSCustomObject]]::new()

function Report-Test {
    param($Num, $Name, $Passed, $Details)
    $obj = [PSCustomObject]@{
        Num = $Num
        Name = $Name
        Passed = $Passed
        Details = $Details
    }
    $results.Add($obj)
    $status = if ($Passed) { "PASS" } else { "FAIL" }
    $color = if ($Passed) { "Green" } else { "Red" }
    Write-Host ("[" + $status + "] TEST " + $Num + ": " + $Name + " | " + $Details) -ForegroundColor $color
}

# Login as Transportation Manager
$loginBody = @{ identifier = "transport@facestone.com"; password = "Password123!" } | ConvertTo-Json
$loginRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginRes.data.token
$headers = @{ "Authorization" = "Bearer $token" }

# Helper to send JSON POST /api/transportation
function Send-TransportationRecord {
    param($payload)
    $bodyJson = $payload | ConvertTo-Json -Depth 5
    $req = [System.Net.HttpWebRequest]::Create("$baseUrl/api/transportation")
    $req.Method = "POST"
    $req.ContentType = "application/json; charset=utf-8"
    $req.Headers["Authorization"] = "Bearer $token"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($bodyJson)
    $req.ContentLength = $bytes.Length

    $stream = $req.GetRequestStream()
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Close()

    try {
        $resp = $req.GetResponse()
        $reader = [System.IO.StreamReader]::new($resp.GetResponseStream(), [System.Text.Encoding]::UTF8)
        $respBody = $reader.ReadToEnd()
        $reader.Close()
        $parsed = $respBody | ConvertFrom-Json
        return @{ StatusCode = [int]$resp.StatusCode; Body = $parsed; Message = $parsed.message; Raw = $respBody }
    } catch [System.Net.WebException] {
        $errResp = $_.Exception.Response
        if ($errResp -ne $null) {
            $reader = [System.IO.StreamReader]::new($errResp.GetResponseStream(), [System.Text.Encoding]::UTF8)
            $errBody = $reader.ReadToEnd()
            $reader.Close()
            $parsed = $null
            try { 
                $parsed = $errBody | ConvertFrom-Json 
            } catch { 
                $parsed = [PSCustomObject]@{ message = $errBody } 
            }
            $msg = if ($parsed.PSObject.Properties['message']) { $parsed.message } elseif ($parsed.PSObject.Properties['title']) { $parsed.title } else { $errBody }
            return @{ StatusCode = [int]$errResp.StatusCode; Body = $parsed; Message = $msg; Raw = $errBody }
        }
        throw
    }
}

Write-Host "================== EXECUTING TRANSPORTATION TEST SUITE ==================" -ForegroundColor Cyan

# TEST 1: Valid PO Lookup -> Company & Destination populated
try {
    $poRes = Invoke-RestMethod -Uri "$baseUrl/api/transportation/po-lookup/PO-2026-FS-101" -Method Get -Headers $headers
    $t1 = ($poRes.success -eq $true -and $poRes.data.poNumber -eq "PO-2026-FS-101" -and !([string]::IsNullOrEmpty($poRes.data.companyName)) -and !([string]::IsNullOrEmpty($poRes.data.destination)))
    Report-Test -Num 1 -Name "Enter Valid PO" -Passed $t1 -Details "PO: $($poRes.data.poNumber), Company: '$($poRes.data.companyName)', Destination: '$($poRes.data.destination)'"
} catch {
    Report-Test -Num 1 -Name "Enter Valid PO" -Passed $false -Details $_.Exception.Message
}

# TEST 2: Invalid PO Lookup -> 404 & 'Purchase Order not found.'
try {
    $invRes = Invoke-RestMethod -Uri "$baseUrl/api/transportation/po-lookup/INVALID-PO-999" -Method Get -Headers $headers
    Report-Test -Num 2 -Name "Enter Invalid PO" -Passed $false -Details "Expected 404 but got success"
} catch [System.Net.WebException] {
    $errResp = $_.Exception.Response
    $msg = ""
    if ($errResp -ne $null) {
        $reader = [System.IO.StreamReader]::new($errResp.GetResponseStream(), [System.Text.Encoding]::UTF8)
        $msg = $reader.ReadToEnd()
        $reader.Close()
    }
    $t2 = ($errResp.StatusCode -eq 404 -and $msg.Contains("Purchase Order not found"))
    Report-Test -Num 2 -Name "Enter Invalid PO" -Passed $t2 -Details "Returned 404: $msg"
} catch {
    Report-Test -Num 2 -Name "Enter Invalid PO" -Passed $false -Details $_.Exception.Message
}

# Base timestamps for testing
$loadedTime = (Get-Date).ToUniversalTime().AddMinutes(-90)
$emptyTime = (Get-Date).ToUniversalTime().AddMinutes(-15)

# Arabic strings constructed safely
$supervisorShip = [System.Text.Encoding]::UTF8.GetString([byte[]](0xd9,0x85,0xd8,0xad,0xd9,0x85,0xd8,0xaf,0x20,0xd9,0x86,0xd8,0xa7,0xd8,0xac,0xd8,0xad)) # محمد ناجح
$supervisorDept = [System.Text.Encoding]::UTF8.GetString([byte[]](0xd8,0xa7,0xd8,0xb2,0xd9,0x87,0xd8,0xb1,0x20,0xd9,0x85,0xd8,0xad,0xd9,0x85,0xd8,0xaf)) # ازهر محمد
$carPlate = [System.Text.Encoding]::UTF8.GetString([byte[]](0xd9,0x85,0x20,0xd8,0xb5,0x20,0xd8,0xb1,0x20,0x37,0x37,0x37)) # م ص ر 777

# TEST 3 & TEST 4 & TEST 6 & TEST 8 & TEST 9 & TEST 10 & TEST 11:
$payload1 = @{
    poNumber = "PO-2026-FS-101"
    transactionDate = [DateTime]::UtcNow.ToString("o")
    carNumber = $carPlate
    driverName = "Mahmoud Hassan"
    driverPhone = "+201012345678"
    materialType = "powder"
    source = "Tampered Client Source Attempt"
    shippingSupervisor = $supervisorShip
    departmentSupervisor = $supervisorDept
    recipientName = "Site Engineer Ahmed"
    recipientTitle = "Site Engineer"
    receivingStatus = "safe"
    receivedDate = [DateTime]::UtcNow.ToString("o")
    departureTime = $loadedTime.ToString("o")
    arrivalTime = $emptyTime.ToString("o")
    internalLoadedWeight = 10000.0
    internalEmptyWeight = 2000.0
    internalLoadedWeightTime = $loadedTime.ToString("o")
    internalEmptyWeightTime = $loadedTime.AddMinutes(-20).ToString("o")
    externalLoadedWeight = 9800.0
    externalEmptyWeight = 2000.0
    externalLoadedWeightTime = $emptyTime.AddMinutes(-10).ToString("o")
    externalEmptyWeightTime = $emptyTime.ToString("o")
}

$res1 = Send-TransportationRecord -payload $payload1
if ($res1.StatusCode -eq 200 -and $res1.Body.success) {
    $data1 = $res1.Body.data
    # TEST 3: Internal Weight
    $t3 = ([Math]::Abs($data1.internalWeight - 8000.0) -lt 0.01)
    Report-Test -Num 3 -Name "Internal Weight Formula" -Passed $t3 -Details "10,000 - 2,000 = $($data1.internalWeight) (Expected 8,000)"

    # TEST 4: External Weight, Waste, and Net Weight
    $t4Ext = ([Math]::Abs($data1.externalNetWeight - 7800.0) -lt 0.01)
    $t4Waste = ([Math]::Abs($data1.wasteQuantity - 200.0) -lt 0.01)
    $t4Net = ([Math]::Abs($data1.netWeight - 7800.0) -lt 0.01)
    $t4 = ($t4Ext -and $t4Waste -and $t4Net)
    Report-Test -Num 4 -Name "External Weight, Waste, Net Weight" -Passed $t4 -Details "External: $($data1.externalNetWeight), Waste: $($data1.wasteQuantity) (Expected 200), Net: $($data1.netWeight) (Expected 7800)"

    # TEST 6: Quantity = Net Weight
    $t6 = ([Math]::Abs($data1.netWeight - 7800.0) -lt 0.01)
    Report-Test -Num 6 -Name "Quantity Equals Net Weight" -Passed $t6 -Details "Quantity (NetWeight): $($data1.netWeight)"

    # TEST 8: Source fixed to Face stone (Full Concrete Solutions)
    $t8 = ($data1.source -eq "Face stone (Full Concrete Solutions)")
    Report-Test -Num 8 -Name "Source Fixed and Server-Enforced" -Passed $t8 -Details "Source is: '$($data1.source)'"

    # TEST 9: Shipping time = External Empty Timestamp - Internal Loaded Timestamp
    $expectedMinutes = [int][Math]::Round(($emptyTime - $loadedTime).TotalMinutes)
    $t9 = ($data1.shippingDurationMinutes -eq $expectedMinutes)
    Report-Test -Num 9 -Name "Shipping Time Calculation" -Passed $t9 -Details "Duration: $($data1.shippingDurationMinutes) mins (Expected: $expectedMinutes mins)"

    # TEST 10: Reference codes unique and auto-generated
    $t10 = (![string]::IsNullOrEmpty($data1.internalWeighbridgeCode) -and ![string]::IsNullOrEmpty($data1.externalWeighbridgeCode) -and ($data1.internalWeighbridgeCode -ne $data1.externalWeighbridgeCode))
    Report-Test -Num 10 -Name "Weighbridge Reference Codes Unique" -Passed $t10 -Details "Internal Code: $($data1.internalWeighbridgeCode), External Code: $($data1.externalWeighbridgeCode)"

    # TEST 11: Transaction Number unique and auto-generated
    $t11 = ($data1.transactionNumber -gt 0 -and $data1.id -gt 0)
    Report-Test -Num 11 -Name "Transaction Number Auto-Generated" -Passed $t11 -Details "Id: $($data1.id), TransactionNumber: $($data1.transactionNumber)"
} else {
    Report-Test -Num 3 -Name "Internal Weight Formula" -Passed $false -Details $res1.Message
    Report-Test -Num 4 -Name "External Weight, Waste, Net Weight" -Passed $false -Details $res1.Message
    Report-Test -Num 6 -Name "Quantity Equals Net Weight" -Passed $false -Details $res1.Message
    Report-Test -Num 8 -Name "Source Fixed and Server-Enforced" -Passed $false -Details $res1.Message
    Report-Test -Num 9 -Name "Shipping Time Calculation" -Passed $false -Details $res1.Message
    Report-Test -Num 10 -Name "Weighbridge Reference Codes Unique" -Passed $false -Details $res1.Message
    Report-Test -Num 11 -Name "Transaction Number Auto-Generated" -Passed $false -Details $res1.Message
}

# TEST 5: If internal and external weights are equal -> Waste = 0, Net Weight = that value
$payloadEqual = @{
    poNumber = "PO-2026-FS-101"
    transactionDate = [DateTime]::UtcNow.ToString("o")
    carNumber = $carPlate
    driverName = "Ahmed"
    materialType = "slurry"
    shippingSupervisor = $supervisorShip
    departmentSupervisor = $supervisorDept
    receivingStatus = "intact"
    internalLoadedWeight = 12000.0
    internalEmptyWeight = 3000.0
    internalLoadedWeightTime = (Get-Date).ToUniversalTime().AddMinutes(-60).ToString("o")
    internalEmptyWeightTime = (Get-Date).ToUniversalTime().AddMinutes(-70).ToString("o")
    externalLoadedWeight = 12000.0
    externalEmptyWeight = 3000.0
    externalLoadedWeightTime = (Get-Date).ToUniversalTime().AddMinutes(-20).ToString("o")
    externalEmptyWeightTime = (Get-Date).ToUniversalTime().ToString("o")
}
$resEqual = Send-TransportationRecord -payload $payloadEqual
if ($resEqual.StatusCode -eq 200 -and $resEqual.Body.success) {
    $dEqual = $resEqual.Body.data
    $t5Waste = ([Math]::Abs($dEqual.wasteQuantity - 0.0) -lt 0.001)
    $t5Net = ([Math]::Abs($dEqual.netWeight - 9000.0) -lt 0.001)
    $t5 = ($t5Waste -and $t5Net)
    Report-Test -Num 5 -Name "Equal Weights -> Waste=0 & Net=Value" -Passed $t5 -Details "Internal: $($dEqual.internalWeight), External: $($dEqual.externalNetWeight), Waste: $($dEqual.wasteQuantity), Net: $($dEqual.netWeight)"
} else {
    Report-Test -Num 5 -Name "Equal Weights -> Waste=0 & Net=Value" -Passed $false -Details $resEqual.Message
}

# TEST 7: Material Type ONLY allows powder or slurry
$payloadBadMaterial = @{
    poNumber = "PO-2026-FS-101"
    transactionDate = [DateTime]::UtcNow.ToString("o")
    carNumber = "CAR-999"
    driverName = "Ahmed"
    materialType = "arbitrary_cement_type"
    internalLoadedWeight = 5000.0
    internalEmptyWeight = 1000.0
    externalLoadedWeight = 5000.0
    externalEmptyWeight = 1000.0
}
$resBadMaterial = Send-TransportationRecord -payload $payloadBadMaterial
$t7 = ($resBadMaterial.StatusCode -eq 400 -and ($resBadMaterial.Raw.Contains("powder") -or $resBadMaterial.Raw.Contains("slurry")))
Report-Test -Num 7 -Name "Material Type Enforced (Only powder / slurry)" -Passed $t7 -Details "Rejected with status $($resBadMaterial.StatusCode): $($resBadMaterial.Message)"

# Negative weights validation test (Loaded < Empty)
$payloadBadWeights = @{
    poNumber = "PO-2026-FS-101"
    transactionDate = [DateTime]::UtcNow.ToString("o")
    carNumber = "CAR-999"
    driverName = "Ahmed"
    materialType = "powder"
    internalLoadedWeight = 1000.0
    internalEmptyWeight = 5000.0
    externalLoadedWeight = 5000.0
    externalEmptyWeight = 1000.0
}
$resBadWeights = Send-TransportationRecord -payload $payloadBadWeights
$tNegativeWeights = ($resBadWeights.StatusCode -eq 400 -and $resBadWeights.Raw.Length -gt 0)
Write-Host "[-] Negative Weight Test: $($resBadWeights.StatusCode) - $($resBadWeights.Message)" -ForegroundColor Gray

# Negative shipping time validation test
$payloadBadDuration = @{
    poNumber = "PO-2026-FS-101"
    transactionDate = [DateTime]::UtcNow.ToString("o")
    carNumber = "CAR-999"
    driverName = "Ahmed"
    materialType = "powder"
    internalLoadedWeight = 5000.0
    internalEmptyWeight = 1000.0
    internalLoadedWeightTime = (Get-Date).ToUniversalTime().ToString("o")
    externalLoadedWeight = 5000.0
    externalEmptyWeight = 1000.0
    externalEmptyWeightTime = (Get-Date).ToUniversalTime().AddMinutes(-30).ToString("o")
}
$resBadDuration = Send-TransportationRecord -payload $payloadBadDuration
$tNegativeDuration = ($resBadDuration.StatusCode -eq 400 -and $resBadDuration.Raw.Length -gt 0)
Write-Host "[-] Negative Duration Test: $($resBadDuration.StatusCode) - $($resBadDuration.Message)" -ForegroundColor Gray

# TEST 12: Verify UI does NOT contain 'إجمالي الكمية' or 'الكمية المتبقية'
$rawBytes = [System.IO.File]::ReadAllBytes("f:\FaceStoneWebsite\wwwroot\index.html")
$indexContent = [System.Text.Encoding]::UTF8.GetString($rawBytes)
$transStart = $indexContent.IndexOf('id="view-transportation"')
$transEnd = $indexContent.IndexOf('id="view-storage"')
$transHtml = if ($transStart -ge 0 -and $transEnd -gt $transStart) { $indexContent.Substring($transStart, $transEnd - $transStart) } else { "" }

$totalQtyStr = [System.Text.Encoding]::UTF8.GetString([byte[]](0xd8,0xa5,0xd8,0xac,0xd9,0x85,0xd8,0xa7,0xd9,0x84,0xd9,0x8a,0x20,0xd8,0xa7,0xd9,0x84,0xd9,0x83,0xd9,0x85,0xd9,0x8a,0xd8,0xa9)) # إجمالي الكمية
$remQtyStr = [System.Text.Encoding]::UTF8.GetString([byte[]](0xd8,0xa7,0xd9,0x84,0xd9,0x83,0xd9,0x85,0xd9,0x8a,0xd8,0xa9,0x20,0xd8,0xa7,0xd9,0x84,0xd9,0x85,0xd8,0xaa,0xd8,0xa8,0xd9,0x82,0xd9,0x8a,0xd8,0xa9)) # الكمية المتبقية

$hasTotalQty = $transHtml.Contains($totalQtyStr)
$hasRemQty = $transHtml.Contains($remQtyStr)
$t12 = (!$hasTotalQty -and !$hasRemQty)
Report-Test -Num 12 -Name "UI Removed Total & Remaining Quantity" -Passed $t12 -Details "Has Total Qty in Trans UI: $hasTotalQty, Has Remaining Qty in Trans UI: $hasRemQty"

# TEST 13: Verify Finance still works
$accLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body (@{ identifier = "sarah@facestone.com"; password = "Password123!" } | ConvertTo-Json) -ContentType "application/json"
$accHeaders = @{ "Authorization" = "Bearer $($accLogin.data.token)" }
$finData = Invoke-RestMethod -Uri "$baseUrl/api/finance/management-data" -Method Get -Headers $accHeaders
$t13 = ($finData.success -eq $true -and $finData.data.records.Count -gt 0)
Report-Test -Num 13 -Name "Finance Department Intact" -Passed $t13 -Details "Finance management-data returned $($finData.data.records.Count) records"

# TEST 14: Verify Storage still works
$storageLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body (@{ identifier = "storage@facestone.com"; password = "Storage123!" } | ConvertTo-Json) -ContentType "application/json"
$storageHeaders = @{ "Authorization" = "Bearer $($storageLogin.data.token)" }
$storageConfig = Invoke-RestMethod -Uri "$baseUrl/api/storage/config" -Method Get -Headers $storageHeaders
$t14 = ($storageConfig.success -eq $true -and $storageConfig.data.storageLocations.Count -gt 0)
Report-Test -Num 14 -Name "Storage Department Intact" -Passed $t14 -Details "Storage config returned $($storageConfig.data.storageLocations.Count) locations"

# TEST 15: Verify Management still works
$ceoLogin = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body (@{ identifier = "kareem.ceo@facestone.com"; password = "Password123!" } | ConvertTo-Json) -ContentType "application/json"
$ceoHeaders = @{ "Authorization" = "Bearer $($ceoLogin.data.token)" }
$mgmtData = Invoke-RestMethod -Uri "$baseUrl/api/management/data" -Method Get -Headers $ceoHeaders
$t15 = ($mgmtData.success -eq $true -and $mgmtData.data.records.Count -ge 0)
Report-Test -Num 15 -Name "Management Department Intact" -Passed $t15 -Details "Management data endpoint successful"

# TEST 16: Verify Global Chat still works
$chatData = Invoke-RestMethod -Uri "$baseUrl/api/chat/conversations" -Method Get -Headers $ceoHeaders
$t16 = ($chatData.success -eq $true)
Report-Test -Num 16 -Name "Global Chat Intact" -Passed $t16 -Details "Chat conversations endpoint returned success = true"

Write-Host "================== TEST SUMMARY ==================" -ForegroundColor Cyan
$passedCount = ($results | Where-Object { $_.Passed -eq $true }).Count
$totalCount = $results.Count
$summaryColor = if ($passedCount -eq $totalCount) { "Green" } else { "Red" }
Write-Host ("Passed: " + $passedCount + " / " + $totalCount) -ForegroundColor $summaryColor
