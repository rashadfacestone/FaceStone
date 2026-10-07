$cs = "Server=.\SQLEXPRESS01;Database=FaceStone;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 5 Id, PONumber, WeighbridgeAttachmentPath, WeighbridgeFileName, DailySupplyAttachmentPath, DailySupplyFileName, BillOfLadingAttachmentPath, BillOfLadingFileName FROM Transportation ORDER BY Id DESC"
$r = $cmd.ExecuteReader()
Write-Host "--- TRANSPORTATION ---"
while ($r.Read()) {
    Write-Host "Trans #$($r['Id']) | WB_Path: $($r['WeighbridgeAttachmentPath']) | WB_Name: $($r['WeighbridgeFileName']) | DS_Path: $($r['DailySupplyAttachmentPath']) | BOL_Path: $($r['BillOfLadingAttachmentPath'])"
}
$r.Close()

$cmd.CommandText = "SELECT TOP 5 Id, PONumber, WeighbridgeAttachmentPath, WeighbridgeFileName FROM StorageTransactions ORDER BY Id DESC"
$r = $cmd.ExecuteReader()
Write-Host "--- STORAGE ---"
while ($r.Read()) {
    Write-Host "Storage #$($r['Id']) | WB_Path: $($r['WeighbridgeAttachmentPath']) | WB_Name: $($r['WeighbridgeFileName'])"
}
$r.Close()
$conn.Close()
