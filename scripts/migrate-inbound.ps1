param([string]$Server='MIDUKONGU',[switch]$SkipLegacyReconciliation)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
function Open-Database([string]$database) {
    $settings=[System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    $settings.set_DataSource($Server); $settings.set_InitialCatalog($database)
    $settings.set_IntegratedSecurity($true); $settings.set_Encrypt($false); $settings.set_TrustServerCertificate($true)
    $connection=[System.Data.SqlClient.SqlConnection]::new($settings.ConnectionString)
    $connection.Open(); return $connection
}
$procurement=Open-Database 'ProcurementDB'
try {
    $inventory=Open-Database 'InventoryDB'
    try {
        foreach($entry in @(@{Connection=$procurement;File='migrate-procurement-receiving.sql'},@{Connection=$inventory;File='migrate-inbound.sql'})) {
            $command=$entry.Connection.CreateCommand()
            try {
                $command.CommandTimeout=30
                $command.CommandText=Get-Content (Join-Path $root "scripts/sql/$($entry.File)") -Raw
                [void]$command.ExecuteNonQuery()
                Write-Host "Applied $($entry.File)"
            } finally { $command.Dispose() }
        }
        if(-not $SkipLegacyReconciliation) {
            # Read each owning database separately. Never guess a legacy receipt's warehouse.
            $mapping=@{}
            $lookup=$procurement.CreateCommand(); $lookup.CommandText='SELECT PurchaseOrderID,WarehouseID FROM dbo.PurchaseOrder'
            $reader=$lookup.ExecuteReader()
            try { while($reader.Read()){ $mapping[$reader.GetInt64(0)]=$reader.GetInt32(1) } }
            finally { $reader.Dispose(); $lookup.Dispose() }
            $pending=@()
            $lookup=$inventory.CreateCommand(); $lookup.CommandText='SELECT InboundID,PurchaseOrderID FROM dbo.Inbound WHERE WarehouseID IS NULL'
            $reader=$lookup.ExecuteReader()
            try { while($reader.Read()){ $pending+=@{Id=$reader.GetInt64(0);Po=$reader.GetInt64(1)} } }
            finally { $reader.Dispose(); $lookup.Dispose() }
            foreach($receipt in $pending) {
                if(-not $mapping.ContainsKey($receipt.Po)) { Write-Warning "Inbound $($receipt.Id): PO $($receipt.Po) missing; left unchanged."; continue }
                $update=$inventory.CreateCommand()
                try {
                    $update.CommandText='UPDATE dbo.Inbound SET WarehouseID=@warehouse WHERE InboundID=@id AND PurchaseOrderID=@po AND WarehouseID IS NULL'
                    [void]$update.Parameters.Add('@warehouse',[System.Data.SqlDbType]::Int); $update.Parameters['@warehouse'].Value=$mapping[$receipt.Po]
                    [void]$update.Parameters.Add('@id',[System.Data.SqlDbType]::BigInt); $update.Parameters['@id'].Value=$receipt.Id
                    [void]$update.Parameters.Add('@po',[System.Data.SqlDbType]::BigInt); $update.Parameters['@po'].Value=$receipt.Po
                    [void]$update.ExecuteNonQuery()
                    Write-Host "Reconciled Inbound $($receipt.Id) through PO $($receipt.Po)."
                } finally { $update.Dispose() }
            }
        }
    } finally { $inventory.Dispose() }
} finally { $procurement.Dispose() }
