$connectionString = "Server=.\SQLEXPRESS01; Database=FaceStone; Trusted_Connection=True; TrustServerCertificate=True; MultipleActiveResultSets=True;"
Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT Id, PONumber, Supplier, Quantity, RemainingQuantity, PricePerTon, TonPrice, TotalValue, Destination FROM PurchaseOrders"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Output "$($reader['PONumber']) | $($reader['Supplier']) | Qty: $($reader['Quantity']) | Rem: $($reader['RemainingQuantity']) | TonPrice: $($reader['TonPrice']) | Val: $($reader['TotalValue'])"
    }
    $reader.Close()
}
finally {
    $conn.Close()
}
