$proc = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "FaceStone.Web.csproj", "--no-build" -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 6
try {
    # Check port 5000 or ports in launchSettings
    $ports = @(5124, 52564, 5000)
    $healthy = $false
    foreach ($p in $ports) {
        try {
            $res = Invoke-RestMethod -Uri "http://localhost:$p/health" -TimeoutSec 3 -ErrorAction Stop
            Write-Output ("App responsive on port " + $p + ": " + ($res | ConvertTo-Json -Compress))
            $healthy = $true
            break
        } catch {
            # continue checking
        }
    }
    if (-not $healthy) {
        Write-Output "Health check did not respond on expected standard ports, checking running processes."
    }
} finally {
    if ($proc -and -not $proc.HasExited) {
        Stop-Process -Id $proc.Id -Force
        Write-Output "Process stopped cleanly."
    }
}
