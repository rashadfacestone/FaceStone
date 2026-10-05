Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\SQLEXPRESS01; Database=FaceStone; Trusted_Connection=True; TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()

Write-Output "=== PURCHASE ORDERS ==="
$cmd.CommandText = "SELECT Id, PONumber, Supplier, Quantity, RemainingQuantity, PricePerTon, TonPrice, TotalValue FROM PurchaseOrders"
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Output "ID: $($r['Id']) | PO: $($r['PONumber']) | PricePerTon: $($r['PricePerTon']) | TonPrice: $($r['TonPrice']) | Qty: $($r['Quantity']) | Rem: $($r['RemainingQuantity'])"
}
$r.Close()

Write-Output "`n=== TRANSPORTATION PO MATCHING ==="
$cmd.CommandText = @"
SELECT t.PONumber, COUNT(*) AS TxCount,
       SUM(CASE WHEN po.Id IS NOT NULL THEN 1 ELSE 0 END) AS MatchedById,
       SUM(CASE WHEN po2.PONumber IS NOT NULL THEN 1 ELSE 0 END) AS MatchedByPoNum
FROM Transportation t
LEFT JOIN PurchaseOrders po ON t.PurchaseOrderId = po.Id
LEFT JOIN PurchaseOrders po2 ON t.PONumber = po2.PONumber
GROUP BY t.PONumber
"@
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Output "Trans PO: $($r['PONumber']) | Count: $($r['TxCount']) | MatchedById: $($r['MatchedById']) | MatchedByPoNum: $($r['MatchedByPoNum'])"
}
$r.Close()

Write-Output "`n=== STORAGE PO MATCHING ==="
$cmd.CommandText = @"
SELECT s.Action, s.IsExportRelatedToPO, s.PONumber, COUNT(*) AS TxCount,
       SUM(CASE WHEN po.Id IS NOT NULL THEN 1 ELSE 0 END) AS MatchedById,
       SUM(CASE WHEN po2.PONumber IS NOT NULL THEN 1 ELSE 0 END) AS MatchedByPoNum
FROM StorageTransactions s
LEFT JOIN PurchaseOrders po ON s.PurchaseOrderId = po.Id
LEFT JOIN PurchaseOrders po2 ON s.PONumber = po2.PONumber
GROUP BY s.Action, s.IsExportRelatedToPO, s.PONumber
"@
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Output "Action: $($r['Action']) | RelatedToPO: $($r['IsExportRelatedToPO']) | PO: $($r['PONumber']) | Count: $($r['TxCount']) | MatchedById: $($r['MatchedById'])"
}
$r.Close()

Write-Output "`n=== FOREIGN KEYS IN DATABASE ==="
$cmd.CommandText = @"
SELECT 
    fk.name AS FK_name,
    tp.name AS ParentTable,
    cp.name AS ParentColumn,
    tr.name AS ReferencedTable,
    cr.name AS ReferencedColumn
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.tables tp ON fkc.parent_object_id = tp.object_id
INNER JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
INNER JOIN sys.tables tr ON fkc.referenced_object_id = tr.object_id
INNER JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
WHERE tp.name IN ('Transportation', 'StorageTransactions', 'PurchaseOrders', 'FinancePayments')
"@
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Output "$($r['FK_name']): $($r['ParentTable']).$($r['ParentColumn']) -> $($r['ReferencedTable']).$($r['ReferencedColumn'])"
}
$r.Close()

$conn.Close()
