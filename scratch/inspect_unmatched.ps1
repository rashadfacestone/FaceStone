Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\SQLEXPRESS01; Database=FaceStone; Trusted_Connection=True; TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()

Write-Output "=== UNMATCHED TRANSPORTATION ROWS ==="
$cmd.CommandText = "SELECT Id, TransactionNumber, TransactionDate, PONumber, CarNumber, DriverName, NetWeight, WasteQuantity FROM Transportation WHERE PONumber NOT IN (SELECT PONumber FROM PurchaseOrders)"
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Output "Unmatched Trans: Id=$($r['Id']), Tx#=$($r['TransactionNumber']), Date=$($r['TransactionDate']), PO=$($r['PONumber']), Driver=$($r['DriverName'])"
}
$r.Close()

Write-Output "`n=== SAMPLE DATES IN TRANSPORTATION ==="
$cmd.CommandText = "SELECT TOP 10 Id, TransactionNumber, TransactionDate, PONumber, WasteQuantity FROM Transportation ORDER BY TransactionDate DESC"
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Output "Tx: Id=$($r['Id']), Tx#=$($r['TransactionNumber']), Date=$($r['TransactionDate']), PO=$($r['PONumber']), Waste=$($r['WasteQuantity'])"
}
$r.Close()

Write-Output "`n=== SAMPLE DATES IN STORAGE ==="
$cmd.CommandText = "SELECT TOP 10 Id, MovementNumber, TransactionDate, Action, PONumber, IncomingQuantity, OutgoingQuantity, InternalNetWeight, ExternalNetWeight FROM StorageTransactions ORDER BY TransactionDate DESC"
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Output "Storage: Id=$($r['Id']), Mv#=$($r['MovementNumber']), Date=$($r['TransactionDate']), Action=$($r['Action']), PO=$($r['PONumber']), In=$($r['IncomingQuantity']), Out=$($r['OutgoingQuantity']), IntNet=$($r['InternalNetWeight']), ExtNet=$($r['ExternalNetWeight'])"
}
$r.Close()

$conn.Close()
