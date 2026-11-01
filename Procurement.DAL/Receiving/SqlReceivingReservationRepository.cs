using System.Data;
using Microsoft.Data.SqlClient;
using Procurement.BLL.PurchaseOrders;

namespace Procurement.DAL.Receiving;

/// <summary>
/// A durable guard between Inventory receipt creation and PO terminal transitions.
/// Reservations deliberately survive request failures: an uncertain receipt cannot make its PO closable.
/// </summary>
public sealed class SqlReceivingReservationRepository(string connectionString)
{
    public async Task ReserveAsync(long purchaseOrderId, Guid token, int warehouseId, int userId, CancellationToken ct)
    {
        if (token == Guid.Empty) throw new PurchaseOrderValidationException(["Mã yêu cầu tạo phiếu không hợp lệ."]);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();
        var status = await LockOrder(connection, transaction, purchaseOrderId, warehouseId, ct);
        await using (var existing = new SqlCommand("SELECT PurchaseOrderID,WarehouseID FROM dbo.ReceivingReservation WHERE Token=@token", connection, transaction))
        {
            existing.Parameters.Add("@token", SqlDbType.UniqueIdentifier).Value = token;
            await using var reader = await existing.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetInt64(0) != purchaseOrderId || reader.GetInt32(1) != warehouseId)
                    throw new PurchaseOrderConflictException("Mã yêu cầu đã được sử dụng cho đơn mua hàng khác.");
                await reader.DisposeAsync();
                await transaction.CommitAsync(ct);
                return;
            }
        }
        if (!status.Equals("CONFIRMED", StringComparison.OrdinalIgnoreCase))
            throw new PurchaseOrderConflictException("Chỉ được tạo phiếu nhập từ PO đã xác nhận.");
        await using var insert = new SqlCommand("""
            INSERT dbo.ReceivingReservation(Token,PurchaseOrderID,WarehouseID,CreatedByUserID,Status,CreatedAt)
            VALUES(@token,@po,@warehouse,@user,'OPEN',SYSUTCDATETIME());
            UPDATE dbo.PurchaseOrder SET Note=Note WHERE PurchaseOrderID=@po;
            """, connection, transaction);
        insert.Parameters.Add("@token", SqlDbType.UniqueIdentifier).Value = token;
        insert.Parameters.Add("@po", SqlDbType.BigInt).Value = purchaseOrderId;
        insert.Parameters.Add("@warehouse", SqlDbType.Int).Value = warehouseId;
        insert.Parameters.Add("@user", SqlDbType.Int).Value = userId;
        try { await insert.ExecuteNonQueryAsync(ct); }
        catch (SqlException error) when (error.Number is 2601 or 2627)
        { throw new PurchaseOrderConflictException("Mã yêu cầu đang được sử dụng. Vui lòng thử lại với cùng mã."); }
        await transaction.CommitAsync(ct);
    }

    // The caller must obtain immutable terminal-receipt proof from the owning Inventory API first.
    public async Task ReleaseAsync(long purchaseOrderId, Guid token, int warehouseId, CancellationToken ct)
    {
        if (token == Guid.Empty) throw new PurchaseOrderValidationException(["Mã yêu cầu không hợp lệ."]);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();
        await LockOrder(connection, transaction, purchaseOrderId, warehouseId, ct);
        await using var command = new SqlCommand("""
            UPDATE dbo.ReceivingReservation SET Status='RELEASED',ReleasedAt=SYSUTCDATETIME()
            WHERE Token=@token AND PurchaseOrderID=@po AND WarehouseID=@warehouse AND Status='OPEN';
            IF @@ROWCOUNT>0 UPDATE dbo.PurchaseOrder SET Note=Note WHERE PurchaseOrderID=@po;
            SELECT COUNT(*) FROM dbo.ReceivingReservation WHERE Token=@token AND PurchaseOrderID=@po AND WarehouseID=@warehouse;
            """, connection, transaction);
        command.Parameters.Add("@token", SqlDbType.UniqueIdentifier).Value = token;
        command.Parameters.Add("@po", SqlDbType.BigInt).Value = purchaseOrderId;
        command.Parameters.Add("@warehouse", SqlDbType.Int).Value = warehouseId;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1)
            throw new PurchaseOrderConflictException("Không tìm thấy yêu cầu nhận hàng tương ứng.");
        await transaction.CommitAsync(ct);
    }

    private static async Task<string> LockOrder(SqlConnection connection, SqlTransaction transaction, long purchaseOrderId, int warehouseId, CancellationToken ct)
    {
        await using var command = new SqlCommand("SELECT Status,WarehouseID FROM dbo.PurchaseOrder WITH(UPDLOCK,HOLDLOCK) WHERE PurchaseOrderID=@po", connection, transaction);
        command.Parameters.Add("@po", SqlDbType.BigInt).Value = purchaseOrderId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || reader.GetInt32(1) != warehouseId) throw new PurchaseOrderNotFoundException();
        return reader.GetString(0);
    }
}
