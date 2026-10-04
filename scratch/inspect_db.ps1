Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\SQLEXPRESS01; Database=FaceStone; Trusted_Connection=True; TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20261001105431_AddFinanceTransactionsAndPoEnhancements') INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20261001105431_AddFinanceTransactionsAndPoEnhancements', '8.0.0')"
$cmd.ExecuteNonQuery()
Write-Output "Marked 20261001105431_AddFinanceTransactionsAndPoEnhancements as applied."

Write-Output "--- ROWS IN TRANSPORTATION ---"
$cmd.CommandText = "SELECT COUNT(*) FROM Transportation"
Write-Output ("Count: " + $cmd.ExecuteScalar())

Write-Output "--- SAMPLE DRIVERS ---"
$cmd.CommandText = "SELECT DISTINCT DriverName FROM Transportation"
$r2 = $cmd.ExecuteReader()
while ($r2.Read()) {
    Write-Output ("Driver: " + $r2[0].ToString())
}
$r2.Close()

Write-Output "--- PURCHASE ORDERS ---"
$cmd.CommandText = "SELECT PONumber, Supplier, Destination, Quantity, RemainingQuantity FROM PurchaseOrders"
$r3 = $cmd.ExecuteReader()
while ($r3.Read()) {
    Write-Output ("PO: " + $r3[0].ToString() + " | Supplier: " + $r3[1].ToString() + " | Dest: " + $r3[2].ToString() + " | Qty: " + $r3[3].ToString() + " | Rem: " + $r3[4].ToString())
}
$r3.Close()

$conn.Close()
