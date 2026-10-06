$connectionString = "Server=.\SQLEXPRESS01; Database=FaceStone; Trusted_Connection=True; TrustServerCertificate=True; MultipleActiveResultSets=True;"
Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT TOP 5 TransactionNumber, TransactionDate, CarNumber, DriverName, PONumber, MaterialType, NetWeight, WasteQuantity, ReceivingStatus FROM Transportation ORDER BY TransactionDate DESC"
    $reader = $cmd.ExecuteReader()
    Write-Output "--- Top 5 Transportation ---"
    while ($reader.Read()) {
        Write-Output "#$($reader['TransactionNumber']) | $($reader['TransactionDate']) | $($reader['PONumber']) | $($reader['CarNumber']) | $($reader['DriverName']) | $($reader['MaterialType']) | Net: $($reader['NetWeight']) | Waste: $($reader['WasteQuantity']) | Stat: $($reader['ReceivingStatus'])"
    }
    $reader.Close()

    $cmd2 = $conn.CreateCommand()
    $cmd2.CommandText = "SELECT TOP 5 MovementNumber, ReferenceNumber, TransactionDate, Material, Action, PONumber, IncomingQuantity, OutgoingQuantity, ClosingBalance FROM StorageTransactions ORDER BY TransactionDate DESC"
    $reader2 = $cmd2.ExecuteReader()
    Write-Output "--- Top 5 Storage ---"
    while ($reader2.Read()) {
        Write-Output "#$($reader2['MovementNumber']) | Ref: $($reader2['ReferenceNumber']) | $($reader2['TransactionDate']) | $($reader2['Material']) | $($reader2['Action']) | PO: $($reader2['PONumber']) | In: $($reader2['IncomingQuantity']) | Out: $($reader2['OutgoingQuantity']) | Bal: $($reader2['ClosingBalance'])"
    }
    $reader2.Close()
}
finally {
    $conn.Close()
}
