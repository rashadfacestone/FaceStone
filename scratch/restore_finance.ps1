$lines = Get-Content "F:\FaceStoneWebsite\Controllers\FinanceController.cs"
$trimmed = $lines[0..466] + @("}")
$trimmed | Set-Content "F:\FaceStoneWebsite\Controllers\FinanceController.cs" -Encoding UTF8
