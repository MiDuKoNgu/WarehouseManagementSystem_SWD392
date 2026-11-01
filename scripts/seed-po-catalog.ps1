param([string]$Server = 'MIDUKONGU', [switch]$WindowsAuthentication)

$ErrorActionPreference = 'Stop'
$sqlPassword = $null
$secretPointer = [IntPtr]::Zero
$connection = $null
try {
    $settings = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $settings.set_DataSource($Server)
    $settings.set_InitialCatalog('master')
    $settings.set_Encrypt($true)
    $settings.set_TrustServerCertificate($true)
    $settings.set_ConnectTimeout(10)
    if ($WindowsAuthentication) { $settings.set_IntegratedSecurity($true) }
    else {
        [string]$sqlUsername = Read-Host 'SQL Server username (vi du sa)'
        if ([string]::IsNullOrWhiteSpace($sqlUsername)) { throw 'SQL username khong duoc de trong.' }
        $sqlPassword = Read-Host 'SQL Server password' -AsSecureString
        $secretPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sqlPassword)
        $settings.set_UserID($sqlUsername)
        $settings.set_Password([Runtime.InteropServices.Marshal]::PtrToStringBSTR($secretPointer))
    }
    $connection = New-Object System.Data.SqlClient.SqlConnection($settings.ConnectionString)
    $settings.Clear()
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 30
    $command.CommandText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'sql/seed-po-catalog.sql'), [System.Text.Encoding]::UTF8)
    $reader = $command.ExecuteReader()
    do {
        while ($reader.Read()) {
            $values = @()
            for ($index = 0; $index -lt $reader.FieldCount; $index++) { $values += $reader.GetValue($index).ToString() }
            Write-Output ($values -join ' | ')
        }
    } while ($reader.NextResult())
    $reader.Dispose()
    $command.Dispose()
    Write-Output 'Seed danh muc demo hoan tat. Khong tao PO, khong thay doi tai khoan hoac kho.'
} finally {
    if ($null -ne $connection) { $connection.Dispose() }
    if ($secretPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secretPointer) }
    if ($null -ne $sqlPassword) { $sqlPassword.Dispose() }
}
