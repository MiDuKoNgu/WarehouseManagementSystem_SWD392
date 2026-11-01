using Microsoft.Data.SqlClient;
using System.Data;

namespace Inventory.DAL.Receiving;

public sealed record ReservationProof(bool Exists, bool Terminal, long PurchaseOrderId, int WarehouseId);
public sealed record PendingSerial(long SerialItemId, string SerialNumber, int ProductId, long? InboundId,
    string? ReceiptCode, int? ManufacturingMonth, int? ManufacturingYear, string ItemCondition, bool IsQuarantined, DateTime RegisteredAt);

public sealed class ReceiptQueries(string connectionString)
{
    public async Task<ReservationProof> ReservationAsync(Guid token, int warehouseId, CancellationToken ct)
    {
        await using var c = new SqlConnection(connectionString); await c.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT PurchaseOrderID,WarehouseID,Status FROM dbo.Inbound WHERE CreationToken=@token AND WarehouseID=@warehouse", c);
        cmd.Parameters.Add("@token", SqlDbType.UniqueIdentifier).Value = token;
        cmd.Parameters.Add("@warehouse", SqlDbType.Int).Value = warehouseId;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new(true, r.GetString(2).ToUpperInvariant() is "DONE" or "COMPLETED" or "CANCELLED", r.GetInt64(0), r.GetInt32(1)) : new(false, false, 0, warehouseId);
    }
    public async Task<Guid?> TokenAsync(long id, int warehouseId, CancellationToken ct)
    {
        await using var c = new SqlConnection(connectionString); await c.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT CreationToken FROM dbo.Inbound WHERE InboundID=@id AND WarehouseID=@warehouse", c);
        cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = id;
        cmd.Parameters.Add("@warehouse", SqlDbType.Int).Value = warehouseId;
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is Guid token ? token : null;
    }
    public async Task<PendingSerial[]> PendingAsync(int warehouseId, CancellationToken ct)
    {
        await using var c = new SqlConnection(connectionString); await c.OpenAsync(ct);
        await using var cmd = new SqlCommand("""
            SELECT s.SerialItemID,s.SerialNumber,s.ProductID,s.OriginalInboundID,i.ReceiptCode,
                s.ManufacturingMonth,s.ManufacturingYear,s.ItemCondition,s.IsQuarantined,s.RegisteredAt
            FROM dbo.SerialItem s LEFT JOIN dbo.Inbound i ON i.InboundID=s.OriginalInboundID
            WHERE s.CurrentWarehouseID=@warehouse AND s.StockState='IN_WAREHOUSE' AND s.CurrentLocationID IS NULL
                AND ((s.OriginalInboundID IS NULL AND s.OriginType<>'PURCHASE') OR (UPPER(i.Status) IN ('DONE','COMPLETED') AND i.CompletedAt IS NOT NULL))
            ORDER BY s.RegisteredAt,s.SerialItemID
            """, c);
        cmd.Parameters.Add("@warehouse", SqlDbType.Int).Value = warehouseId;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<PendingSerial>();
        while (await r.ReadAsync(ct)) rows.Add(new(r.GetInt64(0),r.GetString(1),r.GetInt32(2),r.IsDBNull(3)?null:r.GetInt64(3),r.IsDBNull(4)?null:r.GetString(4),
            r.IsDBNull(5)?null:Convert.ToInt32(r[5]),r.IsDBNull(6)?null:Convert.ToInt32(r[6]),r.GetString(7),r.GetBoolean(8),DateTime.SpecifyKind(r.GetDateTime(9),DateTimeKind.Utc)));
        return rows.ToArray();
    }
}
