Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.Web

$chromePath = "C:\Program Files\Google\Chrome\Application\chrome.exe"
if (-not (Test-Path $chromePath)) {
    $chromePath = "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
}

$userDataDir = "f:\FaceStoneWebsite\scratch\chrome_cdp_profile"
if (Test-Path $userDataDir) { Remove-Item -Recurse -Force $userDataDir -ErrorAction SilentlyContinue }

$chromeProcess = Start-Process -FilePath $chromePath -ArgumentList @(
    "--remote-debugging-port=9222",
    "--headless=new",
    "--no-sandbox",
    "--disable-gpu",
    "--window-size=1440,900",
    "--user-data-dir=$userDataDir",
    "http://127.0.0.1:5124"
) -PassThru

Start-Sleep -Seconds 3

# Connect to CDP
$http = New-Object System.Net.Http.HttpClient
$versionJson = $http.GetStringAsync("http://127.0.0.1:9222/json").GetAwaiter().GetResult()
$pages = ConvertFrom-Json $versionJson
$wsUrl = $pages[0].webSocketDebuggerUrl

Write-Host "Connected to Chrome CDP: $wsUrl"

# WebSocket client
$ws = New-Object System.Net.WebSockets.ClientWebSocket
$cts = New-Object System.Threading.CancellationTokenSource
$ws.ConnectAsync([Uri]$wsUrl, $cts.Token).GetAwaiter().GetResult()

function Send-CDP ($method, $params = @{}) {
    $id = [System.Threading.Interlocked]::Increment([ref]$global:cdpId)
    $payload = @{
        id = $id
        method = $method
        params = $params
    } | ConvertTo-Json -Depth 10 -Compress
    
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $segment = New-Object System.ArraySegment[byte] -ArgumentList @($bytes, 0, $bytes.Length)
    $ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).GetAwaiter().GetResult()
    
    $buffer = New-Object byte[] 6553600
    $seg = New-Object System.ArraySegment[byte] -ArgumentList @($buffer, 0, $buffer.Length)
    $res = $ws.ReceiveAsync($seg, $cts.Token).GetAwaiter().GetResult()
    $json = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $res.Count)
    return ConvertFrom-Json $json
}

$global:cdpId = 1

function Capture-Screenshot ($filename) {
    $res = Send-CDP "Page.captureScreenshot" @{ format = "png" }
    $base64 = $res.result.data
    $bytes = [System.Convert]::FromBase64String($base64)
    [System.IO.File]::WriteAllBytes("f:\FaceStoneWebsite\scratch\$filename", $bytes)
    Write-Host "Screenshot saved: f:\FaceStoneWebsite\scratch\$filename"
}

function Evaluate-JS ($expr) {
    $res = Send-CDP "Runtime.evaluate" @{ expression = $expr; returnByValue = $true }
    return $res.result.result.value
}

# Wait for page load
Start-Sleep -Seconds 2

# 1. Login Page Screenshot & Check
Write-Host "--- 1. Testing Login Page ---"
Capture-Screenshot "test_01_login_modal.png"
$loginImgExists = Evaluate-JS "!!document.querySelector('.auth-brand-logo-img')"
$loginImgSrc = Evaluate-JS "document.querySelector('.auth-brand-logo-img')?.src"
Write-Host "Login image exists: $loginImgExists, src: $loginImgSrc"

# 2. Login as CEO to view Departments
Write-Host "--- 2. Performing Login ---"
Evaluate-JS "fillDemoAccount('kareem.ceo@facestone.com', 'Password123!'); document.getElementById('auth-login-form').dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }));"
Start-Sleep -Seconds 3

# Check Departments Page
Write-Host "--- 3. Testing Departments Page ---"
Capture-Screenshot "test_02_departments_desktop.png"

$headerWidth = Evaluate-JS "document.querySelector('.dept-selection-header')?.getBoundingClientRect().width"
$windowWidth = Evaluate-JS "window.innerWidth"
$hasHScroll = Evaluate-JS "document.documentElement.scrollWidth > window.innerWidth"
$deptLogoSrc = Evaluate-JS "document.querySelector('.dept-brand-logo-img')?.src"
Write-Host "Departments Header Width: $headerWidth px, Window Width: $windowWidth px"
Write-Host "Horizontal Scroll detected: $hasHScroll"
Write-Host "Dept Logo src: $deptLogoSrc"

# 3. Test Transportation Page
Write-Host "--- 4. Testing Transportation Department ---"
Evaluate-JS "document.getElementById('btn-enter-transportation')?.click();"
Start-Sleep -Seconds 2
Capture-Screenshot "test_03_transportation.png"
$transHeaderWidth = Evaluate-JS "document.querySelector('.trans-page-header')?.getBoundingClientRect().width"
$transLogoSrc = Evaluate-JS "document.querySelector('.trans-page-header .dept-brand-logo-img')?.src"
Write-Host "Transportation Header Width: $transHeaderWidth px, Logo: $transLogoSrc"

# Back to departments
Evaluate-JS "document.getElementById('trans-back-to-departments-btn')?.click();"
Start-Sleep -Seconds 1

# 4. Test Storage Page
Write-Host "--- 5. Testing Storage Department ---"
Evaluate-JS "document.getElementById('btn-enter-storage')?.click();"
Start-Sleep -Seconds 2
Capture-Screenshot "test_04_storage.png"
$storageHeaderWidth = Evaluate-JS "document.querySelector('.storage-page-header')?.getBoundingClientRect().width"
$storageLogoSrc = Evaluate-JS "document.querySelector('.storage-page-header .dept-brand-logo-img')?.src"
Write-Host "Storage Header Width: $storageHeaderWidth px, Logo: $storageLogoSrc"

# Back to departments
Evaluate-JS "document.getElementById('storage-back-to-departments-btn')?.click();"
Start-Sleep -Seconds 1

# 5. Test Management Page
Write-Host "--- 6. Testing Management Department ---"
Evaluate-JS "document.getElementById('btn-enter-management')?.click();"
Start-Sleep -Seconds 2
Capture-Screenshot "test_05_management.png"
$mgmtHeaderWidth = Evaluate-JS "document.querySelector('.mgmt-page-header')?.getBoundingClientRect().width"
$mgmtLogoSrc = Evaluate-JS "document.querySelector('.mgmt-page-header .dept-brand-logo-img')?.src"
Write-Host "Management Header Width: $mgmtHeaderWidth px, Logo: $mgmtLogoSrc"

# Back to departments
Evaluate-JS "document.getElementById('management-back-to-departments-btn')?.click();"
Start-Sleep -Seconds 1

# 6. Test Finance Department Hub
Write-Host "--- 7. Testing Finance Department Hub ---"
Evaluate-JS "document.getElementById('btn-enter-finance')?.click();"
Start-Sleep -Seconds 2
Capture-Screenshot "test_06_finance_hub.png"
$financeHeaderWidth = Evaluate-JS "document.querySelector('.finance-hub-header')?.getBoundingClientRect().width"
$financeLogoSrc = Evaluate-JS "document.querySelector('.finance-hub-header .dept-brand-logo-img')?.src"
Write-Host "Finance Hub Header Width: $financeHeaderWidth px, Logo: $financeLogoSrc"

# Test Finance Mgmt Page
Evaluate-JS "document.getElementById('btn-finance-hub-mgmt')?.click();"
Start-Sleep -Seconds 2
Capture-Screenshot "test_07_finance_mgmt.png"
$financeMgmtHeaderWidth = Evaluate-JS "document.querySelector('.finance-mgmt-header')?.getBoundingClientRect().width"
Write-Host "Finance Mgmt Header Width: $financeMgmtHeaderWidth px"

# Back to departments
Evaluate-JS "document.getElementById('btn-finance-mgmt-to-dept')?.click() || document.getElementById('btn-finance-hub-back')?.click() || (window.ChatApp && window.ChatApp.showDeptSelection());"
Start-Sleep -Seconds 1

# 7. Test Mobile Viewport (390 x 844)
Write-Host "--- 8. Testing Mobile Responsiveness (390x844) ---"
Send-CDP "Emulation.setDeviceMetricsOverride" @{
    width = 390
    height = 844
    deviceScaleFactor = 2
    mobile = $true
} | Out-Null
Start-Sleep -Seconds 1
Capture-Screenshot "test_08_mobile_departments.png"
$mobileHScroll = Evaluate-JS "document.documentElement.scrollWidth > window.innerWidth"
Write-Host "Mobile Horizontal Scroll: $mobileHScroll"

# Clean up
$ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", $cts.Token).GetAwaiter().GetResult()
$ws.Dispose()
$cts.Dispose()
$http.Dispose()

Stop-Process -Id $chromeProcess.Id -Force -ErrorAction SilentlyContinue
Write-Host "=== TEST SUITE COMPLETED SUCCESSFULLY ==="
