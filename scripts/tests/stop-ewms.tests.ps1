$ErrorActionPreference = 'Stop'
$global:ewmsStopped = @()
$testRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$testDll = Join-Path $testRoot 'Identity.API\bin\Debug\net8.0\Identity.API.dll'
function Get-CimInstance {
    param($ClassName, $Filter)
    if ($ClassName -ne 'Win32_Process' -or $Filter -ne "Name = 'dotnet.exe'") { throw 'Unexpected process query.' }
    @(
        [pscustomobject]@{ ProcessId = 800001; CommandLine = ('dotnet "' + $testDll + '" --urls http://localhost:5140') },
        [pscustomobject]@{ ProcessId = 800002; CommandLine = 'dotnet "D:\OtherProject\Identity.API\bin\Debug\net8.0\Identity.API.dll"' },
        [pscustomobject]@{ ProcessId = 800003; CommandLine = ('dotnet "' + $testDll + '.other"') },
        [pscustomobject]@{ ProcessId = 800004; CommandLine = $null }
    )
}
function Stop-Process {
    param($Id, $ErrorAction)
    $global:ewmsStopped += $Id
}
& (Join-Path (Split-Path $PSScriptRoot -Parent) 'stop-ewms.ps1')
if ($global:ewmsStopped.Count -ne 1 -or $global:ewmsStopped[0] -ne 800001) {
    throw 'Stop task must stop only exact backend DLLs from this checkout.'
}
Write-Output 'PASS: stop task targets this checkout only; other projects, suffix matches and unreadable command lines are ignored. No real processes stopped.'
