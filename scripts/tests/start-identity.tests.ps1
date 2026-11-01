$ErrorActionPreference = 'Stop'
$global:identityLauncherTestCalled = $false
$global:identityLauncherTestMode = 'Seed'
$global:identityLauncherTestFailure = $false

# Mock both interactive input and the child process. Never contacts SQL Server.
function Read-Host {
    param([string]$Prompt, [switch]$AsSecureString)
    if ($AsSecureString) { return ConvertTo-SecureString 'fake;password="value' -AsPlainText -Force }
    return [psobject]'fake-sql-user'
}

function dotnet {
    $connection = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($env:ConnectionStrings__Identity)
    if ($connection.UserID -ne 'fake-sql-user' -or $connection.Password -ne 'fake;password="value') {
        throw 'Credential string conversion failed.'
    }
    if ($connection.DataSource -ne 'MIDUKONGU' -or $connection.InitialCatalog -ne 'IdentityDB') {
        throw 'Server or database mismatch.'
    }
    if (-not $connection.Encrypt -or -not $connection.TrustServerCertificate) { throw 'Connection options mismatch.' }
    if ($env:EWMS_IDENTITY_CONNECTION -ne $env:ConnectionStrings__Identity) { throw 'Seed connection mismatch.' }
    if (($global:identityLauncherTestMode -ne 'Api') -ne (($args -join ' ') -match 'Identity.Seed')) { throw 'Wrong project selected.' }
    if (($global:identityLauncherTestMode -eq 'ResetPassword') -ne ($args -contains '--reset-password')) { throw 'Wrong reset mode arguments.' }
    $global:identityLauncherTestCalled = $true
    if ($global:identityLauncherTestFailure) { throw 'Simulated child failure.' }
}

$beforeApi = $env:ConnectionStrings__Identity
$beforeSeed = $env:EWMS_IDENTITY_CONNECTION
try {
    $env:ConnectionStrings__Identity = 'original-api-value'
    $env:EWMS_IDENTITY_CONNECTION = 'original-seed-value'
    foreach ($mode in @('Seed', 'Api', 'ResetPassword')) {
        $global:identityLauncherTestCalled = $false
        $global:identityLauncherTestMode = $mode
        & (Join-Path (Split-Path $PSScriptRoot -Parent) 'start-identity.ps1') -Mode $mode
        if (-not $global:identityLauncherTestCalled) { throw 'Child process was not called.' }
        if ($env:ConnectionStrings__Identity -ne 'original-api-value' -or $env:EWMS_IDENTITY_CONNECTION -ne 'original-seed-value') {
            throw 'Environment was not restored.'
        }
    }
    $global:identityLauncherTestMode = 'Api'
    $global:identityLauncherTestFailure = $true
    try {
        & (Join-Path (Split-Path $PSScriptRoot -Parent) 'start-identity.ps1') -Mode Api
        throw 'Expected child failure.'
    } catch {
        if ($_.Exception.Message -ne 'Simulated child failure.') { throw }
    }
    if ($env:ConnectionStrings__Identity -ne 'original-api-value' -or $env:EWMS_IDENTITY_CONNECTION -ne 'original-seed-value') {
        throw 'Environment was not restored after failure.'
    }
    Write-Output 'PASS: Seed/Api, PSObject username, special characters in password, environment cleanup on success/failure. No database accessed.'
} finally {
    $env:ConnectionStrings__Identity = $beforeApi
    $env:EWMS_IDENTITY_CONNECTION = $beforeSeed
}
