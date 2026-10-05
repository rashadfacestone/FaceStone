$loginBody = @{
    identifier = "sarah@facestone.com"
    password = "Password123!"
} | ConvertTo-Json

$l = Invoke-RestMethod -Uri "http://localhost:5124/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$h = @{ Authorization = "Bearer " + $l.data.token }

$s = Invoke-RestMethod -Uri "http://localhost:5124/api/finance/statistics?date=2026-10-05" -Headers $h
Write-Host "FilterDate: $($s.data.filterDate)"
Write-Host "FilterPo: $($s.data.filterPoNumber)"
Write-Host "Daily Activity: $($s.data.dailyActivity | ConvertTo-Json)"
Write-Host "Cumulative Summary: $($s.data.cumulativePoSummary | Select-Object -ExcludeProperty allPosSummary | ConvertTo-Json)"
Write-Host "Factory Performance: $($s.data.factoryPerformance | ConvertTo-Json)"

$sPo = Invoke-RestMethod -Uri "http://localhost:5124/api/finance/statistics?date=2026-10-05&po=PO-100" -Headers $h
Write-Host "`nPO-100 STATS:"
Write-Host "Cumulative Summary: $($sPo.data.cumulativePoSummary | Select-Object -ExcludeProperty allPosSummary | ConvertTo-Json)"
Write-Host "Factory Performance: $($sPo.data.factoryPerformance | ConvertTo-Json)"
