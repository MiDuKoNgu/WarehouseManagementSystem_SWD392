using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Procurement.BLL.PurchaseOrders;
using Procurement.Models.PurchaseOrders;

namespace Procurement.DAL.PurchaseOrders;

public sealed class SqlPurchaseOrderRepository(string connectionString) : IPurchaseOrderRepository
{
    private const string SelectSql = """
        SELECT p.PurchaseOrderID, p.POCode, p.SupplierID, p.WarehouseID, p.OrderDate, p.ExpectedArrivalDate,
            p.Status, p.CreatedAt, p.CreatedByUserID, p.Note, l.POLineID, l.ProductID, l.OrderedQuantity, l.ClosedQuantity, p.RowVersion, p.ConfirmedAt, p.ClosedAt, p.CloseReason
        FROM dbo.PurchaseOrder p LEFT JOIN dbo.PurchaseOrderLine l ON l.PurchaseOrderID = p.PurchaseOrderID
        """;

    public async Task<StoredPurchaseOrder[]> ListAsync(CancellationToken ct) => await ReadAsync(null, ct);
    public async Task<StoredPurchaseOrder?> FindAsync(long id, CancellationToken ct) => (await ReadAsync(id, ct)).SingleOrDefault();

    private async Task<StoredPurchaseOrder[]> ReadAsync(long? id, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return await ReadAsync(connection, null, id, false, ct);
    }

    private static async Task<StoredPurchaseOrder[]> ReadAsync(SqlConnection connection, SqlTransaction? transaction, long? id, bool locked, CancellationToken ct)
    {
        var sql = locked ? SelectSql.Replace("FROM dbo.PurchaseOrder p", "FROM dbo.PurchaseOrder p WITH (UPDLOCK,HOLDLOCK)") : SelectSql;
        await using var command = new SqlCommand(sql + (id is null ? "" : " WHERE p.PurchaseOrderID = @id")
            + " ORDER BY p.CreatedAt DESC, p.PurchaseOrderID DESC, l.POLineID", connection, transaction);
        if (id is { } value) command.Parameters.Add("@id", SqlDbType.BigInt).Value = value;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var headers = new Dictionary<long, StoredPurchaseOrder>();
        var lines = new Dictionary<long, List<StoredLine>>();
        while (await reader.ReadAsync(ct))
        {
            var orderId = reader.GetInt64(0);
            if (!headers.ContainsKey(orderId))
            {
                headers[orderId] = new(orderId, reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3),
                    DateOnly.FromDateTime(reader.GetDateTime(4)), reader.IsDBNull(5) ? null : DateOnly.FromDateTime(reader.GetDateTime(5)),
                    reader.GetString(6), DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc), reader.GetInt32(8),
                    reader.IsDBNull(9) ? null : reader.GetString(9), [], Convert.ToBase64String((byte[])reader[14]),
                    reader.IsDBNull(15) ? null : DateTime.SpecifyKind(reader.GetDateTime(15), DateTimeKind.Utc),
                    reader.IsDBNull(16) ? null : DateTime.SpecifyKind(reader.GetDateTime(16), DateTimeKind.Utc),
                    reader.IsDBNull(17) ? null : reader.GetString(17));
                lines[orderId] = [];
            }
            if (!reader.IsDBNull(10)) lines[orderId].Add(new(reader.GetInt64(10), reader.GetInt32(11), reader.GetInt32(12), reader.GetInt32(13)));
        }
        return headers.Values.Select(order => order with { Lines = lines[order.PurchaseOrderId].ToArray() }).ToArray();
    }

    public async Task<StoredPurchaseOrder> CreateAsync(CreatePurchaseOrder request, int userId, string userName,
        string action, CancellationToken ct, string? importKey = null)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();
        if (importKey is not null)
        {
            // A durable key in the creation audit also prevents duplicate writes after a commit
            // succeeds but its HTTP response is lost. The app lock serializes matching retries.
            await using var duplicate = new SqlCommand("""
                DECLARE @lockResult int;
                EXEC @lockResult = sys.sp_getapplock @Resource = @lockKey, @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction', @LockTimeout = 10000;
                IF @lockResult < 0 THROW 50002, 'Cannot acquire import lock.', 1;
                SELECT TOP (1) TRY_CONVERT(bigint, EntityID) FROM dbo.AuditLog
                WHERE UserID = @user AND Action = 'IMPORT_PO_EXCEL' AND EntityName = 'PurchaseOrder'
                    AND JSON_VALUE(CASE WHEN ISJSON(NewValues) = 1 THEN NewValues ELSE N'{}' END, '$.importKey') = @importKey
                """, connection, transaction);
            duplicate.Parameters.Add("@lockKey", SqlDbType.NVarChar, 255).Value = $"EWMS:POImport:{userId}:{importKey}";
            duplicate.Parameters.Add("@user", SqlDbType.Int).Value = userId;
            duplicate.Parameters.Add("@importKey", SqlDbType.VarChar, 64).Value = importKey;
            var existing = await duplicate.ExecuteScalarAsync(ct);
            if (existing is not null and not DBNull)
            {
                await transaction.CommitAsync(ct);
                return await FindAsync(Convert.ToInt64(existing), ct)
                    ?? throw new InvalidOperationException("Import audit references a missing PO.");
            }
        }
        // The unique code index provides the final collision guard; GUID suffix avoids MAX()+1 races.
        var now = DateTime.UtcNow;
        var code = $"PO-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..33];
        var orderDate = PurchaseOrderService.Today();
        await using var header = new SqlCommand("""
            INSERT INTO dbo.PurchaseOrder
                (SupplierID, WarehouseID, CreatedByUserID, POCode, OrderDate, ExpectedArrivalDate, Status, Note, CreatedAt)
            OUTPUT INSERTED.PurchaseOrderID
            VALUES (@supplier, @warehouse, @creator, @code, @date, @arrival, 'DRAFT', @note, @createdAt)
            """, connection, transaction);
        header.Parameters.Add("@supplier", SqlDbType.Int).Value = request.SupplierId;
        header.Parameters.Add("@warehouse", SqlDbType.Int).Value = request.WarehouseId;
        header.Parameters.Add("@creator", SqlDbType.Int).Value = userId;
        header.Parameters.Add("@code", SqlDbType.VarChar, 40).Value = code;
        header.Parameters.Add("@date", SqlDbType.Date).Value = orderDate.ToDateTime(TimeOnly.MinValue);
        header.Parameters.Add("@arrival", SqlDbType.Date).Value = request.ExpectedArrivalDate is { } arrival ? arrival.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
        header.Parameters.Add("@note", SqlDbType.NVarChar, 1000).Value = (object?)request.Note ?? DBNull.Value;
        header.Parameters.Add("@createdAt", SqlDbType.DateTime2).Value = now;
        var id = Convert.ToInt64(await header.ExecuteScalarAsync(ct));
        var savedLines = new List<StoredLine>();
        foreach (var line in request.Lines)
        {
            await using var insertLine = new SqlCommand("""
                INSERT INTO dbo.PurchaseOrderLine (PurchaseOrderID, ProductID, OrderedQuantity, ClosedQuantity)
                OUTPUT INSERTED.POLineID VALUES (@po, @product, @quantity, 0)
                """, connection, transaction);
            insertLine.Parameters.Add("@po", SqlDbType.BigInt).Value = id;
            insertLine.Parameters.Add("@product", SqlDbType.Int).Value = line.ProductId;
            insertLine.Parameters.Add("@quantity", SqlDbType.Int).Value = line.Quantity;
            var lineId = Convert.ToInt64(await insertLine.ExecuteScalarAsync(ct));
            savedLines.Add(new(lineId, line.ProductId, line.Quantity));
        }
        await using var audit = new SqlCommand("""
            INSERT INTO dbo.AuditLog (UserID, Action, EntityName, EntityID, OldValues, NewValues, Status, CreatedAt)
            VALUES (@user, @action, 'PurchaseOrder', @entityId, NULL, @values, 'SUCCESS', @time)
            """, connection, transaction);
        audit.Parameters.Add("@user", SqlDbType.Int).Value = userId;
        audit.Parameters.Add("@action", SqlDbType.VarChar, 60).Value = action;
        audit.Parameters.Add("@entityId", SqlDbType.NVarChar, 100).Value = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        audit.Parameters.Add("@values", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(new
        {
            purchaseOrderId = id, poCode = code, importKey, request.SupplierId, request.WarehouseId,
            createdByUserId = userId, createdByName = userName, orderDate,
            request.ExpectedArrivalDate, request.Note, status = "DRAFT", createdAt = now, lines = savedLines
        });
        audit.Parameters.Add("@time", SqlDbType.DateTime2).Value = now;
        await audit.ExecuteNonQueryAsync(ct);
        // Disposing an uncommitted transaction rolls back header, lines and audit on any failure.
        await transaction.CommitAsync(ct);
        return await FindAsync(id, ct) ?? throw new InvalidOperationException("Created PO could not be loaded.");
    }

    public async Task<StoredPurchaseOrder> UpdateAsync(StoredPurchaseOrder expected, CreatePurchaseOrder request, int userId, string userName, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();
        var before = await LockAndCheck(connection, transaction, expected, ct);
        if (!before.Status.Equals("DRAFT", StringComparison.OrdinalIgnoreCase))
            throw new PurchaseOrderConflictException("Chỉ được sửa PO ở trạng thái DRAFT.");
        if (before.Lines.Any(line => line.ClosedQuantity != 0))
            throw new PurchaseOrderConflictException("PO nháp có số lượng đóng không hợp lệ.");
        await using (var header = new SqlCommand("""
            UPDATE dbo.PurchaseOrder SET SupplierID=@supplier, WarehouseID=@warehouse,
                ExpectedArrivalDate=@arrival, Note=@note, Status='DRAFT'
            WHERE PurchaseOrderID=@id
            """, connection, transaction))
        {
            header.Parameters.Add("@supplier", SqlDbType.Int).Value = request.SupplierId;
            header.Parameters.Add("@warehouse", SqlDbType.Int).Value = request.WarehouseId;
            header.Parameters.Add("@arrival", SqlDbType.Date).Value = request.ExpectedArrivalDate is { } date ? date.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            header.Parameters.Add("@note", SqlDbType.NVarChar, 1000).Value = (object?)request.Note ?? DBNull.Value;
            header.Parameters.Add("@id", SqlDbType.BigInt).Value = before.PurchaseOrderId;
            await header.ExecuteNonQueryAsync(ct);
        }
        foreach (var line in before.Lines.Where(line => !request.Lines.Any(item => item.ProductId == line.ProductId)))
        {
            await using var delete = new SqlCommand("DELETE dbo.PurchaseOrderLine WHERE POLineID=@line AND PurchaseOrderID=@po", connection, transaction);
            delete.Parameters.Add("@line", SqlDbType.BigInt).Value = line.PoLineId;
            delete.Parameters.Add("@po", SqlDbType.BigInt).Value = before.PurchaseOrderId;
            await delete.ExecuteNonQueryAsync(ct);
        }
        foreach (var line in request.Lines)
        {
            var existing = before.Lines.SingleOrDefault(item => item.ProductId == line.ProductId);
            await using var write = new SqlCommand(existing is null
                ? "INSERT dbo.PurchaseOrderLine(PurchaseOrderID,ProductID,OrderedQuantity,ClosedQuantity) VALUES(@po,@product,@quantity,0)"
                : "UPDATE dbo.PurchaseOrderLine SET OrderedQuantity=@quantity WHERE PurchaseOrderID=@po AND ProductID=@product", connection, transaction);
            write.Parameters.Add("@po", SqlDbType.BigInt).Value = before.PurchaseOrderId;
            write.Parameters.Add("@product", SqlDbType.Int).Value = line.ProductId;
            write.Parameters.Add("@quantity", SqlDbType.Int).Value = line.Quantity;
            await write.ExecuteNonQueryAsync(ct);
        }
        var after = (await ReadAsync(connection, transaction, before.PurchaseOrderId, false, ct)).Single();
        await WriteAudit(connection, transaction, "UPDATE_PO", before, after, userId, userName, ct);
        await transaction.CommitAsync(ct);
        return after;
    }

    public async Task<StoredPurchaseOrder> TransitionAsync(StoredPurchaseOrder expected, string targetStatus, string? reason,
        IReadOnlyDictionary<long, int> closedQuantities, int userId, string userName, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        using var transaction = connection.BeginTransaction();
        var before = await LockAndCheck(connection, transaction, expected, ct);
        var current = before.Status.ToUpperInvariant();
        var allowed = targetStatus switch { "CONFIRMED" => current == "DRAFT", "CANCELLED" => current is "DRAFT" or "CONFIRMED", "CLOSED" => current == "CONFIRMED", _ => false };
        if (!allowed) throw new PurchaseOrderConflictException("Trạng thái PO không cho phép thao tác này.");
        // This check shares the same PO row lock as receipt reservation. Even an uncertain
        // Inventory request keeps the PO open until its receipt has reached a terminal state.
        if (targetStatus is "CLOSED" or "CANCELLED")
        {
            await using var receipts = new SqlCommand("""
                SELECT COUNT(*) FROM dbo.ReceivingReservation
                WHERE PurchaseOrderID=@po AND (@cancel=1 OR Status='OPEN')
                """, connection, transaction);
            receipts.Parameters.Add("@po", SqlDbType.BigInt).Value = before.PurchaseOrderId;
            receipts.Parameters.Add("@cancel", SqlDbType.Bit).Value = targetStatus == "CANCELLED";
            if (Convert.ToInt32(await receipts.ExecuteScalarAsync(ct)) > 0)
                throw new PurchaseOrderConflictException(targetStatus == "CANCELLED"
                    ? "Không thể hủy PO đã có yêu cầu nhận hàng."
                    : "PO còn yêu cầu nhận hàng đang mở. Hoàn tất hoặc hủy phiếu nhập trước khi đóng PO.");
        }
        if (targetStatus == "CLOSED")
        {
            if (closedQuantities.Count != before.Lines.Length || before.Lines.Length == 0
                || before.Lines.Any(line => !closedQuantities.TryGetValue(line.PoLineId, out var value) || value < 0 || value > line.Quantity || line.ClosedQuantity != 0))
                throw new PurchaseOrderConflictException("Số lượng đóng không khớp các dòng PO.");
            foreach (var line in before.Lines)
            {
                await using var closeLine = new SqlCommand("UPDATE dbo.PurchaseOrderLine SET ClosedQuantity=@quantity WHERE POLineID=@line AND PurchaseOrderID=@po", connection, transaction);
                closeLine.Parameters.Add("@quantity", SqlDbType.Int).Value = closedQuantities[line.PoLineId];
                closeLine.Parameters.Add("@line", SqlDbType.BigInt).Value = line.PoLineId;
                closeLine.Parameters.Add("@po", SqlDbType.BigInt).Value = before.PurchaseOrderId;
                await closeLine.ExecuteNonQueryAsync(ct);
            }
        }
        await using (var update = new SqlCommand("""
            UPDATE dbo.PurchaseOrder SET Status=@status,
                ConfirmedAt=CASE WHEN @status='CONFIRMED' THEN @now ELSE ConfirmedAt END,
                ClosedAt=CASE WHEN @status IN ('CANCELLED','CLOSED') THEN @now ELSE ClosedAt END,
                CloseReason=CASE WHEN @status IN ('CANCELLED','CLOSED') THEN @reason ELSE CloseReason END
            WHERE PurchaseOrderID=@id
            """, connection, transaction))
        {
            update.Parameters.Add("@status", SqlDbType.VarChar, 32).Value = targetStatus;
            update.Parameters.Add("@now", SqlDbType.DateTime2).Value = DateTime.UtcNow;
            update.Parameters.Add("@reason", SqlDbType.NVarChar, 1000).Value = (object?)reason ?? DBNull.Value;
            update.Parameters.Add("@id", SqlDbType.BigInt).Value = before.PurchaseOrderId;
            await update.ExecuteNonQueryAsync(ct);
        }
        var after = (await ReadAsync(connection, transaction, before.PurchaseOrderId, false, ct)).Single();
        var action = targetStatus switch { "CONFIRMED" => "CONFIRM_PO", "CANCELLED" => "CANCEL_PO", _ => "CLOSE_PO" };
        await WriteAudit(connection, transaction, action, before, after, userId, userName, ct);
        await transaction.CommitAsync(ct);
        return after;
    }

    private static async Task<StoredPurchaseOrder> LockAndCheck(SqlConnection connection, SqlTransaction transaction, StoredPurchaseOrder expected, CancellationToken ct)
    {
        var current = (await ReadAsync(connection, transaction, expected.PurchaseOrderId, true, ct)).SingleOrDefault()
            ?? throw new PurchaseOrderNotFoundException();
        if (current.RowVersion != expected.RowVersion)
            throw new PurchaseOrderConflictException("PO đã được thay đổi bởi thao tác khác. Vui lòng tải lại.");
        return current;
    }

    private static async Task WriteAudit(SqlConnection connection, SqlTransaction transaction, string action,
        StoredPurchaseOrder before, StoredPurchaseOrder after, int userId, string name, CancellationToken ct)
    {
        await using var command = new SqlCommand("""
            INSERT dbo.AuditLog(UserID,Action,EntityName,EntityID,OldValues,NewValues,Status,CreatedAt)
            VALUES(@user,@action,'PurchaseOrder',@id,@old,@new,'SUCCESS',@at)
            """, connection, transaction);
        command.Parameters.Add("@user", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@action", SqlDbType.VarChar, 60).Value = action;
        command.Parameters.Add("@id", SqlDbType.NVarChar, 100).Value = after.PurchaseOrderId.ToString();
        command.Parameters.Add("@old", SqlDbType.NVarChar, -1).Value = Snapshot(before, null);
        command.Parameters.Add("@new", SqlDbType.NVarChar, -1).Value = Snapshot(after, name);
        command.Parameters.Add("@at", SqlDbType.DateTime2).Value = DateTime.UtcNow;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string Snapshot(StoredPurchaseOrder order, string? actor) => JsonSerializer.Serialize(new
    {
        purchaseOrderId = order.PurchaseOrderId, poCode = order.PoCode, order.SupplierId, order.WarehouseId,
        order.CreatedByUserId, order.OrderDate, order.ExpectedArrivalDate, order.Note,
        status = order.Status.ToUpperInvariant(), order.ConfirmedAt, order.ClosedAt, order.CloseReason,
        performedByName = actor, lines = order.Lines
    });
}
