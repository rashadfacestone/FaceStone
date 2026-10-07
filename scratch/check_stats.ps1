$cmLogin = Invoke-RestMethod -Uri "http://localhost:5124/api/auth/login" -Method Post -Body (@{
    identifier = "control_monitoring"
    password = "Password123!"
} | ConvertTo-Json) -ContentType "application/json"
$cmToken = $cmLogin.data.token
$headers = @{ Authorization = "Bearer $cmToken" }

$stats = (Invoke-RestMethod -Uri "http://localhost:5124/api/control-monitoring/stats" -Method Get -Headers $headers).data
Write-Host "Pending count: $($stats.pendingCount)"
Write-Host "Approved today: $($stats.approvedTodayCount)"
Write-Host "Rejected today: $($stats.rejectedTodayCount)"
Write-Host "Total verified: $($stats.totalVerifiedCount)"
Write-Host "Transportation pending: $($stats.transportationPendingCount)"
Write-Host "Storage pending: $($stats.storagePendingCount)"
