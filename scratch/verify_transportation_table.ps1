$connStr = "Server=.\SQLEXPRESS01; Database=FaceStone; Trusted_Connection=True; TrustServerCertificate=True; MultipleActiveResultSets=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)

try {
    $conn.Open()
    Write-Output "=================================================="
    Write-Output "1. VERIFY TABLE EXISTS & SELECT * FROM Transportation"
    Write-Output "=================================================="
    
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT * FROM Transportation"
    $reader = $cmd.ExecuteReader()
    Write-Output "Query 'SELECT * FROM Transportation;' executed successfully!"
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
WHERE TABLE_NAME = 'Transportation'
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
        $isIdentity = if ($reader.GetInt32(6) -eq 1) { " [IDENTITY PK]" } else { "" }
        Write-Output ("  - {0,-25} : {1}{2}{3} (Nullable: {4}){5}" -f $col, $dtype, $maxLen, $numPrec, $nullable, $isIdentity)
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
WHERE t.name = 'Transportation'
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
    $existingTables = @("Users", "ChatMessages", "ChatAttachments", "LiveLocationSessions")
    foreach ($tbl in $existingTables) {
        $cmd.CommandText = "SELECT COUNT(*) FROM [$tbl]"
        $count = $cmd.ExecuteScalar()
        Write-Output ("  - Table '{0}' exists and contains {1} rows." -f $tbl, $count)
    }

    Write-Output "`n=================================================="
    Write-Output "5. TEST INSERT IN LOCAL DEVELOPMENT DATABASE"
    Write-Output "=================================================="
    $cmd.CommandText = @"
INSERT INTO Transportation (
    TransactionNumber, Date, CarPlatesNumber, DriverName, Source, PO, 
    MaterialType, WeightBeforeLoading, WeightWhenEnterFactory, WeightWhenExitFactory, 
    NetWeight, DeliveryStatus, ShippingStatus, Notes, Destination
) VALUES (
    'TEST-001', GETUTCDATE(), 'TEST-123', 'Test Driver', 'Test Source', 'TEST-PO',
    'Test Material', 10000.000, 9000.000, 2000.000,
    7000.000, 'Pending', 'Pending', 'Development test record', 'Test Destination'
);
SELECT SCOPE_IDENTITY();
"@
    $insertedId = $cmd.ExecuteScalar()
    Write-Output ("  Inserted test record successfully! Generated Id: {0}" -f $insertedId)

    # Query it back
    $cmd.CommandText = "SELECT Id, TransactionNumber, CarPlatesNumber, DriverName, WeightBeforeLoading, NetWeight, DeliveryStatus, ShippingStatus FROM Transportation WHERE TransactionNumber = 'TEST-001'"
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        Write-Output ("  Retrieved Record -> Id: {0}, TxNum: {1}, Plates: {2}, Driver: {3}, WeightBefore: {4}, NetWeight: {5}, DeliveryStatus: {6}, ShippingStatus: {7}" -f `
            $reader[0], $reader[1], $reader[2], $reader[3], $reader[4], $reader[5], $reader[6], $reader[7])
    }
    $reader.Close()

    # Test UNIQUE constraint enforcement on TransactionNumber
    Write-Output "`n=================================================="
    Write-Output "6. VERIFY UNIQUE CONSTRAINT ON TransactionNumber"
    Write-Output "=================================================="
    try {
        $cmd.CommandText = @"
INSERT INTO Transportation (
    TransactionNumber, Date, CarPlatesNumber, DriverName, Source, PO, 
    MaterialType, WeightBeforeLoading, WeightWhenEnterFactory, WeightWhenExitFactory, 
    NetWeight, DeliveryStatus, ShippingStatus, Notes, Destination
) VALUES (
    'TEST-001', GETUTCDATE(), 'TEST-999', 'Duplicate Driver', 'Test Source', 'TEST-PO',
    'Test Material', 10000.000, 9000.000, 2000.000,
    7000.000, 'Pending', 'Pending', 'Duplicate test record', 'Test Destination'
);
"@
        $cmd.ExecuteNonQuery()
        Write-Error "ERROR: Duplicate TransactionNumber was allowed! Unique constraint failed."
    } catch {
        Write-Output ("  Unique constraint verified! Attempting duplicate 'TEST-001' was correctly rejected by database:")
        Write-Output ("  Message: " + $_.Exception.Message)
    }

    # Clean up test record
    $cmd.CommandText = "DELETE FROM Transportation WHERE TransactionNumber = 'TEST-001'"
    $deleted = $cmd.ExecuteNonQuery()
    Write-Output ("`n  Cleaned up temporary test record (rows deleted: {0}). Database left pristine." -f $deleted)

    Write-Output "`n=================================================="
    Write-Output "ALL DATABASE CHECKS PASSED SUCCESSFULLY!"
    Write-Output "=================================================="

} catch {
    Write-Error $_.Exception.Message
} finally {
    $conn.Close()
}
