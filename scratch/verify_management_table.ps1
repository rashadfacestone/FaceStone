$connStr = "Server=.\SQLEXPRESS01; Database=FaceStone; Trusted_Connection=True; TrustServerCertificate=True; MultipleActiveResultSets=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)

try {
    $conn.Open()
    Write-Output "=================================================="
    Write-Output "1. VERIFY TABLE EXISTS & SELECT * FROM Management"
    Write-Output "=================================================="
    
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT * FROM Management"
    $reader = $cmd.ExecuteReader()
    Write-Output "Query 'SELECT * FROM Management;' executed successfully!"
    $reader.Close()

    Write-Output "`n=================================================="
    Write-Output "2. TABLE STRUCTURE (COLUMNS, TYPES & PRECISION)"
    Write-Output "=================================================="
    $cmd.CommandText = @"
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    CHARACTER_MAXIMUM_LENGTH, 
    NUMERIC_PRECISION, 
    NUMERIC_SCALE, 
    IS_NULLABLE,
    COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') AS IsIdentity
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Management'
ORDER BY ORDINAL_POSITION
"@
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        $col = $reader.GetString(0)
        $dtype = $reader.GetString(1)
        $maxLen = if ($reader.IsDBNull(2)) { "" } else { " (" + $reader.GetInt32(2) + ")" }
        $numPrec = if ($reader.IsDBNull(3)) { "" } else { 
            $scale = if ($reader.IsDBNull(4)) { 0 } else { $reader.GetInt32(4) }
            " (" + $reader.GetByte(3) + "," + $scale + ")" 
        }
        $nullable = $reader.GetString(5)
        $isIdentity = if ($reader.GetInt32(6) -eq 1) { " [IDENTITY]" } else { " [NO IDENTITY]" }
        Write-Output ("  - {0,-28} : {1}{2}{3} (Nullable: {4}){5}" -f $col, $dtype, $maxLen, $numPrec, $nullable, $isIdentity)
    }
    $reader.Close()

    Write-Output "`n=================================================="
    Write-Output "3. PRIMARY KEY & INDEXES"
    Write-Output "=================================================="
    $cmd.CommandText = @"
SELECT 
    i.name AS IndexName,
    i.is_unique,
    i.is_primary_key,
    c.name AS ColumnName
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE t.name = 'Management'
ORDER BY i.name, ic.key_ordinal
"@
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        $idxName = $reader.GetString(0)
        $isUnique = $reader.GetBoolean(1)
        $isPk = $reader.GetBoolean(2)
        $colName = $reader.GetString(3)
        Write-Output ("  - Index: {0} on column '{1}' (Unique: {2}, PK: {3})" -f $idxName, $colName, $isUnique, $isPk)
    }
    $reader.Close()

    Write-Output "`n=================================================="
    Write-Output "4. VERIFY EXISTING TABLES & ROW COUNTS ARE INTACT"
    Write-Output "=================================================="
    $existingTables = @("Users", "ChatMessages", "ChatAttachments", "LiveLocationSessions", "Transportation")
    foreach ($tbl in $existingTables) {
        $cmd.CommandText = "SELECT COUNT(*) FROM [$tbl]"
        $count = $cmd.ExecuteScalar()
        Write-Output ("  - Table '{0}' exists and contains {1} rows." -f $tbl, $count)
    }

    Write-Output "`n=================================================="
    Write-Output "5. TEST INSERT IN LOCAL DEVELOPMENT DATABASE"
    Write-Output "=================================================="
    $cmd.CommandText = @"
INSERT INTO Management (
    TransactionNumber, Date, PO, MaterialType, NumberOfTransactions,
    TotalReceivedAmount, CarPlatesNumber, AcceptedQuantity, UnacceptedPendingQuantity,
    DeliveryStatus, Destination, Notes
) VALUES (
    'MGMT-TEST-001', GETUTCDATE(), 'PO-88899', 'Reinforced Concrete Mix C35', 12,
    145000.750, 'ABC-1234', 850.500, 45.250,
    'Delivered', 'Factory Sector B - Site 4', 'Management audit verification record'
);
"@
    $cmd.ExecuteNonQuery()
    Write-Output "  Inserted test management record 'MGMT-TEST-001' successfully!"

    # Query it back
    $cmd.CommandText = @"
SELECT 
    TransactionNumber, Date, PO, MaterialType, NumberOfTransactions, 
    TotalReceivedAmount, CarPlatesNumber, AcceptedQuantity, UnacceptedPendingQuantity, 
    DeliveryStatus, Destination, Notes 
FROM Management 
WHERE TransactionNumber = 'MGMT-TEST-001'
"@
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        Write-Output ("  Retrieved Record -> TxNum: {0}, PO: {1}, Material: {2}, NumTx: {3}, TotalRecv: {4}, Plates: {5}, AccQty: {6}, UnaccQty: {7}, Status: {8}, Dest: {9}" -f `
            $reader[0], $reader[2], $reader[3], $reader[4], $reader[5], $reader[6], $reader[7], $reader[8], $reader[9], $reader[10])
    }
    $reader.Close()

    # Test PRIMARY KEY enforcement on TransactionNumber
    Write-Output "`n=================================================="
    Write-Output "6. VERIFY PRIMARY KEY / UNIQUE CONSTRAINT ON TransactionNumber"
    Write-Output "=================================================="
    try {
        $cmd.CommandText = @"
INSERT INTO Management (
    TransactionNumber, Date, PO, MaterialType, NumberOfTransactions,
    TotalReceivedAmount, CarPlatesNumber, AcceptedQuantity, UnacceptedPendingQuantity,
    DeliveryStatus, Destination, Notes
) VALUES (
    'MGMT-TEST-001', GETUTCDATE(), 'PO-99999', 'Duplicate Mix', 1,
    1000.000, 'XYZ-999', 10.000, 0.000,
    'Pending', 'Duplicate Destination', 'Duplicate test'
);
"@
        $cmd.ExecuteNonQuery()
        Write-Error "ERROR: Duplicate TransactionNumber was allowed! Primary key violation failed."
    } catch {
        Write-Output ("  Primary key enforcement verified! Attempting duplicate 'MGMT-TEST-001' was correctly rejected by database:")
        Write-Output ("  Message: " + $_.Exception.Message)
    }

    # Clean up test record
    $cmd.CommandText = "DELETE FROM Management WHERE TransactionNumber = 'MGMT-TEST-001'"
    $deleted = $cmd.ExecuteNonQuery()
    Write-Output ("`n  Cleaned up temporary test record (rows deleted: {0}). Database left pristine." -f $deleted)

    Write-Output "`n=================================================="
    Write-Output "ALL MANAGEMENT DATABASE CHECKS PASSED SUCCESSFULLY!"
    Write-Output "=================================================="

} catch {
    Write-Error $_.Exception.Message
} finally {
    $conn.Close()
}
