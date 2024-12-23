# Create base test directories
$baseDir = "C:\temp\converter_test"
$sqlServerDir = "$baseDir\SQLServer"
$mariaDbDir = "$baseDir\MariaDB"

# Create directories if they don't exist
New-Item -ItemType Directory -Force -Path $sqlServerDir
New-Item -ItemType Directory -Force -Path $mariaDbDir

# Sample SQL Server files with uppercase naming
$sqlServerFiles = @{
    "VT_Customer.cs" = @"
public partial class VT_Customer
{
    public int CustomerID { get; set; }
    public string CustomerName { get; set; }
}
"@
    "VT_Order.cs" = @"
public partial class VT_Order
{
    public int OrderID { get; set; }
    public VT_Customer Customer { get; set; }
}
"@
}

# Corresponding MariaDB files with lowercase naming
$mariaDbFiles = @{
    "vt_customer.cs" = @"
public partial class vt_customer
{
    public int CustomerID { get; set; }
    public string CustomerName { get; set; }
}
"@
    "vt_order.cs" = @"
public partial class vt_order
{
    public int OrderID { get; set; }
    public vt_customer Customer { get; set; }
}
"@
}

# Create SQL Server files
foreach ($file in $sqlServerFiles.Keys) {
    $sqlServerFiles[$file] | Out-File -FilePath "$sqlServerDir\$file" -Encoding UTF8
}

# Create MariaDB files
foreach ($file in $mariaDbFiles.Keys) {
    $mariaDbFiles[$file] | Out-File -FilePath "$mariaDbDir\$file" -Encoding UTF8
}

Write-Host "Test directories and files created at $baseDir"
Write-Host "SQLServer directory: $sqlServerDir"
Write-Host "MariaDB directory: $mariaDbDir"