param(
    [string]$Server = 'MIDUKONGU',
    [switch]$WindowsAuthentication,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$solutionRoot = Split-Path $PSScriptRoot -Parent
$services = @(
    @{ Name = 'Identity'; Port = 5140 },
    @{ Name = 'Catalog'; Port = 5254 },
    @{ Name = 'Warehouse'; Port = 5104 },
    @{ Name = 'Procurement'; Port = 5136 },
    @{ Name = 'Inventory'; Port = 5159 },
    @{ Name = 'WMS'; Port = 5062 }
)

# Do not stop an existing user-owned process. Ask the user to close its terminal first.
foreach ($service in $services) {
    $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, $service.Port)
    try { $listener.Start() }
    catch { throw "Port $($service.Port) dang duoc dung. Hay chay task 'EWMS: Stop backend' hoac scripts/stop-ewms.ps1 truoc khi chay lai." }
    finally { $listener.Stop() }
}

if (-not $SkipBuild) {
    foreach ($service in $services) {
        $project = Join-Path $solutionRoot "$($service.Name).API/$($service.Name).API.csproj"
        dotnet build $project --nologo -m:1 -p:UseSharedCompilation=false
        if ($LASTEXITCODE -ne 0) { throw "Build $($service.Name).API that bai." }
    }
}

$sqlPassword = $null
$secretPointer = [IntPtr]::Zero
$children = @()
$oldEnvironment = @{}
$environmentNames = @('ASPNETCORE_ENVIRONMENT', 'ConnectionStrings__Identity', 'ConnectionStrings__Catalog', 'ConnectionStrings__Warehouse', 'ConnectionStrings__Procurement', 'ConnectionStrings__Inventory')
foreach ($name in $environmentNames) { $oldEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $settings = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $settings.set_DataSource($Server)
    $settings.set_Encrypt($true)
    $settings.set_TrustServerCertificate($true)
    $settings.set_ConnectTimeout(15)
    if ($WindowsAuthentication) {
        $settings.set_IntegratedSecurity($true)
    } else {
        [string]$sqlUsername = Read-Host 'SQL Server username (vi du sa, khong phai ten server)'
        if ([string]::IsNullOrWhiteSpace($sqlUsername)) { throw 'SQL username khong duoc de trong.' }
        $sqlPassword = Read-Host 'SQL Server password' -AsSecureString
        $secretPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sqlPassword)
        $settings.set_UserID($sqlUsername)
        $settings.set_Password([Runtime.InteropServices.Marshal]::PtrToStringBSTR($secretPointer))
    }
    foreach ($service in $services) {
        if ($service.Name -eq 'WMS') { continue } # Gateway reads APIs; it owns no database.
        $settings.set_InitialCatalog("$($service.Name)DB")
        [Environment]::SetEnvironmentVariable("ConnectionStrings__$($service.Name)", $settings.ConnectionString, 'Process')
    }
    $settings.Clear()
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $logDirectory = Join-Path $solutionRoot '.local/logs'
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    $runId = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    foreach ($service in $services) {
        $projectDirectory = Join-Path $solutionRoot "$($service.Name).API"
        $dll = Join-Path $projectDirectory "bin/Debug/net8.0/$($service.Name).API.dll"
        $log = Join-Path $logDirectory "$runId-$($service.Name)"
        $process = Start-Process -FilePath 'dotnet' -ArgumentList @(('"' + $dll + '"'), '--urls', "http://localhost:$($service.Port)") -WorkingDirectory $projectDirectory -WindowStyle Hidden -PassThru -RedirectStandardOutput "$log.log" -RedirectStandardError "$log.error.log"
        $children += $process
        Write-Host "$($service.Name).API: http://localhost:$($service.Port) (PID $($process.Id))"
    }
    # Child processes have inherited their settings. Remove credentials from this parent immediately.
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $oldEnvironment[$name], 'Process') }
    Write-Host "Backend dang chay. Log: $logDirectory"
    Write-Host 'Giu cua so nay mo; Ctrl+C de dung cac backend do lenh nay khoi dong.'
    while ($true) {
        foreach ($process in $children) {
            if ($process.HasExited) { throw "Backend PID $($process.Id) da dung. Xem log tai $logDirectory." }
        }
        Start-Sleep -Seconds 2
    }
} finally {
    foreach ($process in $children) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
    }
    if ($secretPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secretPointer) }
    if ($null -ne $sqlPassword) { $sqlPassword.Dispose() }
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $oldEnvironment[$name], 'Process') }
}
