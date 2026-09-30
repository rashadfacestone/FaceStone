$connStr = "Server=.\SQLEXPRESS01; Database=FaceStone; Trusted_Connection=True; TrustServerCertificate=True; MultipleActiveResultSets=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
try {
    $conn.Open()
    Write-Output "=== Connection Succeeded ==="
    
    # 1. Base tables
    Write-Output "`n=== BASE TABLES ==="
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Output ("Table: " + $reader.GetString(0) + "." + $reader.GetString(1))
    }
    $reader.Close()

    # 2. Check if EF Migrations History table exists
    Write-Output "`n=== EF MIGRATIONS HISTORY ==="
    $cmd.CommandText = "IF OBJECT_ID('__EFMigrationsHistory', 'U') IS NOT NULL SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory ELSE SELECT 'None', 'None'"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Output ("Migration: " + $reader.GetString(0) + " (Version: " + $reader.GetString(1) + ")")
    }
    $reader.Close()

    # 3. Columns for each table
    Write-Output "`n=== COLUMNS FOR ALL TABLES ==="
    $cmd.CommandText = "SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS ORDER BY TABLE_NAME, ORDINAL_POSITION"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        $maxLen = if ($reader.IsDBNull(3)) { "" } else { " (" + $reader.GetInt32(3) + ")" }
        $numPrec = if ($reader.IsDBNull(4)) { "" } else { 
            $scale = if ($reader.IsDBNull(5)) { 0 } else { $reader.GetInt32(5) }
            " (" + $reader.GetByte(4) + "," + $scale + ")" 
        }
        Write-Output ("  " + $reader.GetString(0) + "." + $reader.GetString(1) + " : " + $reader.GetString(2) + $maxLen + $numPrec + " Nullable=" + $reader.GetString(6))
    }
    $reader.Close()

    # 4. Foreign Keys
    Write-Output "`n=== FOREIGN KEYS ==="
    $cmd.CommandText = @"
SELECT 
    fk.name AS FK_Name,
    tp.name AS ParentTable,
    cp.name AS ParentColumn,
    tr.name AS ReferencedTable,
    cr.name AS ReferencedColumn
FROM sys.foreign_keys fk
INNER JOIN sys.tables tp ON fk.parent_object_id = tp.object_id
INNER JOIN sys.tables tr ON fk.referenced_object_id = tr.object_id
INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
INNER JOIN sys.columns cp ON fkc.parent_column_id = cp.column_id AND fkc.parent_object_id = cp.object_id
INNER JOIN sys.columns cr ON fkc.referenced_column_id = cr.column_id AND fkc.referenced_object_id = cr.object_id
ORDER BY tp.name, fk.name
"@
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Output ("  FK: " + $reader.GetString(0) + " : " + $reader.GetString(1) + "(" + $reader.GetString(2) + ") -> " + $reader.GetString(3) + "(" + $reader.GetString(4) + ")")
    }
    $reader.Close()

    # 5. Unique Constraints and Indexes
    Write-Output "`n=== INDEXES AND CONSTRAINTS ==="
    $cmd.CommandText = @"
SELECT 
    t.name AS TableName,
    i.name AS IndexName,
    i.is_unique,
    i.is_primary_key,
    c.name AS ColumnName
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE t.is_ms_shipped = 0
ORDER BY t.name, i.name, ic.key_ordinal
"@
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Output ("  Index: " + $reader.GetString(0) + "." + $reader.GetString(1) + " on " + $reader.GetString(4) + " (Unique=" + $reader.GetBoolean(2) + ", PK=" + $reader.GetBoolean(3) + ")")
    }
    $reader.Close()

} catch {
    Write-Error $_.Exception.Message
} finally {
    $conn.Close()
}
