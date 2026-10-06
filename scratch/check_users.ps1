$connectionString = "Server=.\SQLEXPRESS01; Database=FaceStone; Trusted_Connection=True; TrustServerCertificate=True; MultipleActiveResultSets=True;"
Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT TOP 10 FullName, Username, Email, PhoneNumber, Role FROM Users ORDER BY FullName"
    $reader = $cmd.ExecuteReader()
    Write-Output "--- Users ---"
    while ($reader.Read()) {
        Write-Output "$($reader['FullName']) | $($reader['Username']) | $($reader['Email']) | $($reader['Role'])"
    }
    $reader.Close()
}
finally {
    $conn.Close()
}
