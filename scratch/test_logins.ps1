$baseUrl = 'http://localhost:5124'
foreach ($id in @('transport@facestone.com', 'kareem.ceo@facestone.com', 'sarah@facestone.com', 'storage@facestone.com', 'it@facestone.com')) {
    $body = @{ identifier = $id; password = 'Password123!' } | ConvertTo-Json
    try {
        $res = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $body -ContentType 'application/json'
        Write-Host "$id : SUCCESS - $($res.data.user.role)"
    } catch {
        Write-Host "$id : FAIL - $($_.Exception.Message)"
    }
}
