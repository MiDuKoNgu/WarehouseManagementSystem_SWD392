$ErrorActionPreference = 'Stop'
$solutionRoot = Split-Path $PSScriptRoot -Parent
$services = @('Identity', 'Catalog', 'Warehouse', 'Procurement', 'Inventory', 'WMS')
$processes = @(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'")
foreach ($process in $processes) {
    foreach ($service in $services) {
        $dll = Join-Path $solutionRoot "$service.API\bin\Debug\net8.0\$service.API.dll"
        # Only the exact DLL launched by start-ewms.ps1 in this checkout is owned here.
        if ($process.CommandLine -and $process.CommandLine.IndexOf(('"' + $dll + '"'), [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            try { Stop-Process -Id $process.ProcessId -ErrorAction Stop }
            catch {
                # The launcher may already have cleaned up this child. Surface real failures.
                if (Get-Process -Id $process.ProcessId -ErrorAction SilentlyContinue) { throw }
                break
            }
            Write-Host "Stopped $service.API (PID $($process.ProcessId))"
            break
        }
    }
}
