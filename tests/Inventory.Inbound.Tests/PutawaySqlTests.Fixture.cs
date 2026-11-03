using System.Data;
using Inventory.BLL.Putaway;
using Inventory.DAL.Putaway;
using Inventory.Models.Putaway;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Inbound.Tests;

/// <summary>All writes and failure-injection DDL run only in three GUID-named disposable fixture databases.</summary>
internal static partial class PutawaySqlTests
{

    private sealed class Fixture(string connectionString, string catalogDatabase, string warehouseDatabase)
    {
        public PutawayRepository Repository { get; } = new(connectionString, new CurrentSchemaPutawayGuard(), catalogDatabase, warehouseDatabase);
        public Task<PutawaySerial> Find(long id, PutawayActor? actor = null) => Repository.FindAsync(actor ?? ActorA, id, null, Ct);
        public async Task<PutawayResult> Confirm(long id, int locationId, PutawayActor? actor = null)
        {
            actor ??= ActorA;
            return await Repository.ConfirmAsync(actor, new(id, locationId, (await Find(id, actor)).RowVersion), Ct);
        }
        public async Task Execute(string sql)
        {
            await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(Ct);
            await PutawaySqlTests.Execute(connection, sql);
        }
        public async Task WarehouseExecute(string sql)
        {
            var settings = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = warehouseDatabase };
            await using var connection = new SqlConnection(settings.ConnectionString); await connection.OpenAsync(Ct);
            await PutawaySqlTests.Execute(connection, sql);
        }
        public async Task<T> Scalar<T>(string sql)
        {
            await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(Ct);
            await using var command = new SqlCommand(sql, connection);
            return (T)(await command.ExecuteScalarAsync(Ct))!;
        }
        public async Task Insert(long id, int productId, int warehouseId, string condition = "GOOD", bool quarantine = false,
            string? serialNumber = null, long? inboundId = -1, string origin = "PURCHASE")
        {
            await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(Ct);
            await using var command = new SqlCommand("""
                INSERT dbo.SerialItem(SerialItemID,SerialNumber,ProductID,StockState,ItemCondition,IsQuarantined,CurrentWarehouseID,
                    CurrentLocationID,OriginalInboundID,OriginalAdjustmentID,CurrentSupplySourceID,RegisteredAt,OriginType)
                VALUES(@id,@serial,@product,'IN_WAREHOUSE',@condition,@quarantine,@warehouse,NULL,@inbound,NULL,1,SYSUTCDATETIME(),@origin)
                """, connection);
            command.Parameters.Add("@id", SqlDbType.BigInt).Value = id;
            command.Parameters.Add("@serial", SqlDbType.NVarChar, 100).Value = serialNumber ?? $"SERIAL-{id}";
            command.Parameters.Add("@product", SqlDbType.Int).Value = productId;
            command.Parameters.Add("@condition", SqlDbType.VarChar, 8).Value = condition;
            command.Parameters.Add("@quarantine", SqlDbType.Bit).Value = quarantine;
            command.Parameters.Add("@warehouse", SqlDbType.Int).Value = warehouseId;
            command.Parameters.Add("@inbound", SqlDbType.BigInt).Value = inboundId == -1 ? warehouseId : (object?)inboundId ?? DBNull.Value;
            command.Parameters.Add("@origin", SqlDbType.VarChar, 20).Value = origin;
            await command.ExecuteNonQueryAsync(Ct);
        }
        public async Task Seed()
        {
            await Execute("INSERT dbo.Inbound VALUES(1,'PN-1','DONE',SYSUTCDATETIME(),10),(2,'PN-2','DONE',SYSUTCDATETIME(),10),(3,'PN-DRAFT','DRAFT',NULL,0); INSERT dbo.SupplySource VALUES(1,20,20);");
            for (var warehouse = 1; warehouse <= 2; warehouse++)
                for (var product = 1; product <= 6; product++) await Insert(warehouse * 100 + product, product, warehouse);
        }
    }
}
