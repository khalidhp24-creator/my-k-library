Remove-Item 'Library.mdb' -ErrorAction Ignore
$catalog = New-Object -ComObject ADOX.Catalog
$dbPath = "c:\Users\K\Desktop\My C1 Proj\Library.mdb"
$password = "dbpass"
$connStr = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=$dbPath;Jet OLEDB:Database Password=$password;"
$catalog.Create($connStr)

$conn = New-Object System.Data.OleDb.OleDbConnection($connStr)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "CREATE TABLE Books (ID AUTOINCREMENT PRIMARY KEY, Title VARCHAR(255), Author VARCHAR(255))"
$cmd.ExecuteNonQuery()
$conn.Close()

Write-Host "Database $dbPath created successfully."
