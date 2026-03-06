$cs = "Server=BT-FIKRI\SQLEXPRESS;Database=Operax;User Id=sa;Password=***REMOVED***;TrustServerCertificate=True"
$sql = Get-Content "$PSScriptRoot\seed_complete.sql" -Raw

$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
$conn.Open()

$cmd = New-Object System.Data.SqlClient.SqlCommand($sql, $conn)
$reader = $cmd.ExecuteReader()

Write-Host "=== Eklenen Urunler ==="
while ($reader.Read()) {
    Write-Host "$($reader['Code']) | $($reader['Name']) | Aktif: $($reader['IsActive'])"
}
$reader.Close()
$conn.Close()
Write-Host "Tamamlandi!"
