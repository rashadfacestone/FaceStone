$connStr = "Server=.\SQLEXPRESS01; Database=FaceStone; Trusted_Connection=True; TrustServerCertificate=True; MultipleActiveResultSets=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

Write-Host "=== USERS ==="
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT FullName, Email, Role FROM Users"
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Host "$($r['FullName']) | $($r['Email']) | $($r['Role'])"
}
$r.Close()

Write-Host "`n=== TRANSPORTATION ==="
$cmd.CommandText = "SELECT TransactionNumber, PONumber, Source, MaterialType, NetWeight, ReceivingStatus, TransactionDate FROM Transportation"
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Host "Tx: $($r['TransactionNumber']) | PO: $($r['PONumber']) | Src: $($r['Source']) | Mat: $($r['MaterialType']) | Net: $($r['NetWeight']) | Status: $($r['ReceivingStatus']) | Date: $($r['TransactionDate'])"
}
$r.Close()

Write-Host "`n=== MANAGEMENT ==="
$cmd.CommandText = "SELECT TransactionNumber, PO, TotalReceivedAmount, AcceptedQuantity, UnacceptedPendingQuantity, Date FROM Management"
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Host "MgmtTx: $($r['TransactionNumber']) | PO: $($r['PO']) | Tot: $($r['TotalReceivedAmount']) | Acc: $($r['AcceptedQuantity']) | Unacc: $($r['UnacceptedPendingQuantity']) | Date: $($r['Date'])"
}
$r.Close()

Write-Host "`n=== STORAGE TRANSACTIONS ==="
$cmd.CommandText = "SELECT MovementNumber, PONumber, IncomingQuantity, OutgoingQuantity, TransactionDate FROM StorageTransactions"
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Host "Mov: $($r['MovementNumber']) | PO: $($r['PONumber']) | In: $($r['IncomingQuantity']) | Out: $($r['OutgoingQuantity']) | Date: $($r['TransactionDate'])"
}
$r.Close()

$conn.Close()
