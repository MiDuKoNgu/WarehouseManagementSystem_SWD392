param(
    [ValidateSet('Api', 'Seed', 'ResetPassword')][string]$Mode = 'Api',
    [string]$Server = 'MIDUKONGU',
    [string]$Database = 'IdentityDB'
)

$ErrorActionPreference = 'Stop'
[string]$sqlUsername = Read-Host 'SQL Server username (giong trong SSMS, khong phai ten server)'
if ([string]::IsNullOrWhiteSpace($sqlUsername)) { throw 'SQL username khong duoc de trong.' }
$sqlPassword = Read-Host 'SQL Server password' -AsSecureString
$secretPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sqlPassword)
$previousApiConnection = $env:ConnectionStrings__Identity
$previousSeedConnection = $env:EWMS_IDENTITY_CONNECTION
try {
    # Typed properties avoid Windows PowerShell 5.1 wrapping Read-Host output as
    # PSObject when assigning to the generic builder's object-valued indexer.
    $settings = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $settings.set_DataSource($Server)
    $settings.set_InitialCatalog($Database)
    $settings.set_UserID($sqlUsername)
    $settings.set_Password([Runtime.InteropServices.Marshal]::PtrToStringBSTR($secretPointer))
    $settings.set_Encrypt($true)
    # Local development SQL Server only.
    $settings.set_TrustServerCertificate($true)
    $settings.set_ConnectTimeout(15)
    $env:ConnectionStrings__Identity = $settings.ConnectionString
    $env:EWMS_IDENTITY_CONNECTION = $settings.ConnectionString
    $settings.Clear()
    $solutionRoot = Split-Path $PSScriptRoot -Parent
    if ($Mode -eq 'ResetPassword') {
        dotnet run --project (Join-Path $solutionRoot 'Identity.Seed/Identity.Seed.csproj') -- --reset-password
    } elseif ($Mode -eq 'Seed') {
        dotnet run --project (Join-Path $solutionRoot 'Identity.Seed/Identity.Seed.csproj')
    } else {
        dotnet run --project (Join-Path $solutionRoot 'Identity.API/Identity.API.csproj') --launch-profile http
    }
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secretPointer)
    $sqlPassword.Dispose()
    $env:ConnectionStrings__Identity = $previousApiConnection
    $env:EWMS_IDENTITY_CONNECTION = $previousSeedConnection
}
