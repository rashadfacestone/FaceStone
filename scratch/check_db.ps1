$connectionString = "Server=.\SQLEXPRESS01; Database=FaceStone; Trusted_Connection=True; TrustServerCertificate=True; MultipleActiveResultSets=True;"
Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = @"
SELECT 'Users' as Tbl, COUNT(*) as Cnt FROM Users
UNION ALL SELECT 'Transportation', COUNT(*) FROM Transportation
UNION ALL SELECT 'StorageTransactions', COUNT(*) FROM StorageTransactions
UNION ALL SELECT 'PurchaseOrders', COUNT(*) FROM PurchaseOrders
UNION ALL SELECT 'Expenses', COUNT(*) FROM Expenses
UNION ALL SELECT 'BankTransactions', COUNT(*) FROM BankTransactions
UNION ALL SELECT 'EmployeeFinancialRecords', COUNT(*) FROM EmployeeFinancialRecords
UNION ALL SELECT 'EmployeeLendings', COUNT(*) FROM EmployeeLendings
UNION ALL SELECT 'AttendanceRecords', COUNT(*) FROM AttendanceRecords
"@
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Output "$($reader['Tbl']): $($reader['Cnt'])"
    }
    $reader.Close()
}
catch {
    Write-Output "Error: $_"
}
finally {
    $conn.Close()
}
