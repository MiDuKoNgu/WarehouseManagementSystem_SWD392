using Inventory.Models.Receiving;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Inventory.DAL.Receiving;

public interface IReceivingSummaryRepository
{ Task<PurchaseOrderReceivingSummary> GetAsync(long purchaseOrderId, CancellationToken ct); }

public sealed class ReceivingSummaryRepository(string connectionString) : IReceivingSummaryRepository
{
    public async Task<PurchaseOrderReceivingSummary> GetAsync(long purchaseOrderId, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        // One serial is counted once per PO line across completed receipt batches; canceled scans are excluded.
        await using var command = new SqlCommand("""
            SELECT COUNT(*),COALESCE(SUM(CASE WHEN UPPER(Status)<>'CANCELLED' AND
                (UPPER(Status) NOT IN ('DONE','COMPLETED') OR CompletedAt IS NULL) THEN 1 ELSE 0 END),0)
            FROM dbo.Inbound WHERE PurchaseOrderID=@po;
            SELECT item.POLineID,COUNT(DISTINCT item.SerialItemID)
            FROM dbo.Inbound receipt JOIN dbo.InboundItem item ON item.InboundID=receipt.InboundID
            JOIN dbo.SerialItem serial ON serial.SerialItemID=item.SerialItemID
            WHERE receipt.PurchaseOrderID=@po AND UPPER(receipt.Status) IN ('DONE','COMPLETED')
                AND receipt.CompletedAt IS NOT NULL AND item.IsActive=1
            GROUP BY item.POLineID;
            SELECT CASE WHEN EXISTS (
                SELECT item.SerialItemID
                FROM dbo.Inbound receipt JOIN dbo.InboundItem item ON item.InboundID=receipt.InboundID
                WHERE receipt.PurchaseOrderID=@po AND UPPER(receipt.Status) IN ('DONE','COMPLETED')
                  AND receipt.CompletedAt IS NOT NULL AND item.IsActive=1 AND item.SerialItemID IS NOT NULL
                GROUP BY item.SerialItemID HAVING COUNT(DISTINCT item.POLineID)>1
            ) OR EXISTS (
                SELECT 1 FROM dbo.Inbound receipt JOIN dbo.InboundItem item ON item.InboundID=receipt.InboundID
                LEFT JOIN dbo.SerialItem serial ON serial.SerialItemID=item.SerialItemID
                WHERE receipt.PurchaseOrderID=@po AND UPPER(receipt.Status) IN ('DONE','COMPLETED')
                  AND receipt.CompletedAt IS NOT NULL AND item.IsActive=1 AND serial.SerialItemID IS NULL
            ) THEN 1 ELSE 0 END;
            """, connection);
        command.Parameters.Add("@po", SqlDbType.BigInt).Value = purchaseOrderId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        var any = reader.GetInt32(0) > 0;
        var open = reader.GetInt32(1);
        await reader.NextResultAsync(ct);
        var lines = new List<ReceivingLine>();
        while (await reader.ReadAsync(ct)) lines.Add(new(reader.GetInt64(0), reader.GetInt32(1)));
        await reader.NextResultAsync(ct);
        await reader.ReadAsync(ct);
        return new(purchaseOrderId, any, open, lines.ToArray(), reader.GetInt32(0) == 1);
    }
}
