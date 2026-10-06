[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$loginBody = @{
    identifier = "kareem.ceo@facestone.com"
    password = "Password123!"
} | ConvertTo-Json

$loginRes = Invoke-RestMethod -Uri "http://127.0.0.1:5124/api/auth/login" -Method Post -ContentType "application/json" -Body $loginBody
$token = $loginRes.data.token
$headers = @{ "Authorization" = "Bearer $token" }

$queries = @("PO-100", "Ahmed", "WB", "5000", "powder", "Suez", "مصر")
foreach ($q in $queries) {
    $url = "http://127.0.0.1:5124/api/management/search?q=" + [System.Uri]::EscapeDataString($q)
    $res = Invoke-RestMethod -Uri $url -Method Get -Headers $headers
    $data = $res.data
    Write-Host "`n==============================================="
    Write-Host "QUERY: '$q'"
    Write-Host "==============================================="
    Write-Host "PurchaseOrders count : $($data.purchaseOrders.Count)"
    if ($data.purchaseOrders.Count -gt 0) {
        $p = $data.purchaseOrders[0]
        Write-Host "   -> [$($p.actionLabel)] $($p.title): $($p.subtitle)"
    }
    Write-Host "Transportation count : $($data.transportation.Count)"
    if ($data.transportation.Count -gt 0) {
        $t = $data.transportation[0]
        Write-Host "   -> [$($t.actionLabel)] $($t.title): $($t.subtitle)"
    }
    Write-Host "Storage count        : $($data.storage.Count)"
    if ($data.storage.Count -gt 0) {
        $s = $data.storage[0]
        Write-Host "   -> [$($s.actionLabel)] $($s.title): $($s.subtitle)"
    }
    Write-Host "Finance count        : $($data.finance.Count)"
    if ($data.finance.Count -gt 0) {
        $f = $data.finance[0]
        Write-Host "   -> [$($f.actionLabel)] $($f.title): $($f.subtitle)"
    }
    Write-Host "Employees count      : $($data.employees.Count)"
    if ($data.employees.Count -gt 0) {
        $e = $data.employees[0]
        Write-Host "   -> [$($e.actionLabel)] $($e.title): $($e.subtitle)"
    }
    Write-Host "Suppliers count      : $($data.suppliers.Count)"
    if ($data.suppliers.Count -gt 0) {
        $sup = $data.suppliers[0]
        Write-Host "   -> [$($sup.actionLabel)] $($sup.title): $($sup.subtitle)"
    }
}
