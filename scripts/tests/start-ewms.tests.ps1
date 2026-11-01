$ErrorActionPreference = 'Stop'
$global:ewmsLauncherState = @{ Processes = @(); Stopped = @(); WindowsAuth = $false }

function New-Object {
    param([string]$TypeName, [object[]]$ArgumentList)
    if ($TypeName -eq 'System.Net.Sockets.TcpListener') {
        $listener = [pscustomobject]@{}
        $listener | Add-Member ScriptMethod Start { }
        $listener | Add-Member ScriptMethod Stop { }
        return $listener
    }
    Microsoft.PowerShell.Utility\New-Object @PSBoundParameters
}

function Read-Host {
    param([string]$Prompt, [switch]$AsSecureString)
    if ($AsSecureString) { return ConvertTo-SecureString 'fake;secret="value' -AsPlainText -Force }
    return [psobject]'fake-sql-user'
}

function Start-Process {
    param($FilePath, $ArgumentList, $WorkingDirectory, $WindowStyle, [switch]$PassThru, $RedirectStandardOutput, $RedirectStandardError)
    if ($FilePath -ne 'dotnet' -or $WindowStyle -ne 'Hidden') { throw 'Expected hidden dotnet process.' }
    if (($ArgumentList -join ' ') -match 'fake-sql-user|fake;secret') { throw 'Credentials leaked in command-line arguments.' }
    foreach ($name in @('Identity', 'Catalog', 'Warehouse', 'Procurement', 'Inventory')) {
        $connection = New-Object System.Data.SqlClient.SqlConnectionStringBuilder([Environment]::GetEnvironmentVariable("ConnectionStrings__$name", 'Process'))
        if ($connection.InitialCatalog -ne "${name}DB" -or $connection.DataSource -ne 'MIDUKONGU') { throw 'Wrong database configuration.' }
        if ($global:ewmsLauncherState.WindowsAuth) {
            if (-not $connection.IntegratedSecurity) { throw 'Expected Windows authentication.' }
        } elseif ($connection.UserID -ne 'fake-sql-user' -or $connection.Password -ne 'fake;secret="value') { throw 'SQL credential conversion failed.' }
    }
    $process = [pscustomobject]@{ Id = 800001 + $global:ewmsLauncherState.Processes.Count; HasExited = $false }
    $global:ewmsLauncherState.Processes += $process
    return $process
}

function Start-Sleep {
    param($Seconds)
    if ($env:ConnectionStrings__Identity -ne 'parent-sentinel') { throw 'Credentials remain in parent after launch.' }
    $global:ewmsLauncherState.Processes[0].HasExited = $true
}

function Stop-Process {
    param($Id, $ErrorAction)
    if ($Id -notin $global:ewmsLauncherState.Processes.Id) { throw 'Attempt to stop a process not created by launcher.' }
    $global:ewmsLauncherState.Stopped += $Id
}

function dotnet { throw 'Unexpected build with SkipBuild enabled.' }

$before = $env:ConnectionStrings__Identity
try {
    $env:ConnectionStrings__Identity = 'parent-sentinel'
    foreach ($windows in @($false, $true)) {
        $global:ewmsLauncherState = @{ Processes = @(); Stopped = @(); WindowsAuth = $windows }
        try {
            & (Join-Path (Split-Path $PSScriptRoot -Parent) 'start-ewms.ps1') -SkipBuild -WindowsAuthentication:$windows
            throw 'Expected simulated child termination.'
        } catch {
            if ($_.Exception.Message -notmatch 'Backend PID 800001 da dung') { throw }
        }
        if ($global:ewmsLauncherState.Processes.Count -ne 6 -or $global:ewmsLauncherState.Stopped.Count -ne 5) { throw 'Child lifecycle cleanup failed.' }
        if ($env:ConnectionStrings__Identity -ne 'parent-sentinel') { throw 'Environment not restored after child termination.' }
    }
    Write-Output 'PASS: six-service launch configuration, SQL/Windows auth, no credentials in arguments, parent environment restoration, cleanup of owned children only. No services or database connections started.'
} finally {
    $env:ConnectionStrings__Identity = $before
}
