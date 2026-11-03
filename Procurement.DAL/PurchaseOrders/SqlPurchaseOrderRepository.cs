using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Procurement.BLL.PurchaseOrders;
using Procurement.Models.Entities;
using Procurement.Models.PurchaseOrders;
using OrderEntity = Procurement.Models.Entities.PurchaseOrder;
using LineEntity = Procurement.Models.Entities.PurchaseOrderLine;

namespace Procurement.DAL.PurchaseOrders;

// The historical type name remains for callers; persistence is implemented by EF Core.
public sealed class SqlPurchaseOrderRepository(IDbContextFactory<ProcurementDbContext> contexts) : IPurchaseOrderRepository
{
    public SqlPurchaseOrderRepository(string connectionString) : this(new ProcurementContextFactory(connectionString)) { }

    public async Task<StoredPurchaseOrder[]> ListAsync(CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var orders = await db.PurchaseOrders.AsNoTracking().Include(x => x.Lines)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.PurchaseOrderId).ToArrayAsync(ct);
        return orders.Select(ToStored).ToArray();
    }

    public async Task<StoredPurchaseOrder?> FindAsync(long id, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var order = await db.PurchaseOrders.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.PurchaseOrderId == id, ct);
        return order is null ? null : ToStored(order);
    }

    public async Task<StoredPurchaseOrder> CreateAsync(CreatePurchaseOrder request, int userId, string userName,
        string action, CancellationToken ct, string? importKey = null)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (importKey is not null)
        {
            // Retain SQL Server's transaction-scoped lock and JSON lookup for durable import retries.
            var lockKey = $"EWMS:POImport:{userId}:{importKey}";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @lockResult int;
                EXEC @lockResult = sys.sp_getapplock @Resource = {lockKey}, @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction', @LockTimeout = 10000;
                IF @lockResult < 0 THROW 50002, 'Cannot acquire import lock.', 1;
                """, ct);
            const string emptyJson = "{}";
            var existing = await db.AuditLogs.FromSqlInterpolated($"""
                SELECT * FROM dbo.AuditLog
                WHERE UserID = {userId} AND Action = 'IMPORT_PO_EXCEL' AND EntityName = 'PurchaseOrder'
                    AND JSON_VALUE(CASE WHEN ISJSON(NewValues) = 1 THEN NewValues ELSE {emptyJson} END, '$.importKey') = {importKey}
                """).AsNoTracking().Select(x => x.EntityId).FirstOrDefaultAsync(ct);
            if (long.TryParse(existing, NumberStyles.Integer, CultureInfo.InvariantCulture, out var existingId))
            {
                var prior = await db.PurchaseOrders.AsNoTracking().Include(x => x.Lines)
                    .SingleOrDefaultAsync(x => x.PurchaseOrderId == existingId, ct)
                    ?? throw new InvalidOperationException("Import audit references a missing PO.");
                await transaction.CommitAsync(ct);
                return ToStored(prior);
            }
        }

        var now = DateTime.UtcNow;
        var order = new OrderEntity
        {
            PoCode = $"PO-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..33], SupplierId = request.SupplierId,
            WarehouseId = request.WarehouseId, CreatedByUserId = userId, OrderDate = PurchaseOrderService.Today(),
            ExpectedArrivalDate = request.ExpectedArrivalDate, Note = request.Note, Status = "DRAFT", CreatedAt = now,
            Lines = request.Lines.Select(x => new LineEntity { ProductId = x.ProductId, OrderedQuantity = x.Quantity }).ToList()
        };
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync(ct);
        var result = ToStored(order);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = action, EntityName = "PurchaseOrder", EntityId = order.PurchaseOrderId.ToString(CultureInfo.InvariantCulture),
            CreatedAt = now, NewValues = JsonSerializer.Serialize(new
            {
                purchaseOrderId = order.PurchaseOrderId, poCode = order.PoCode, importKey, request.SupplierId, request.WarehouseId,
                createdByUserId = userId, createdByName = userName, orderDate = order.OrderDate,
                request.ExpectedArrivalDate, request.Note, status = "DRAFT", createdAt = now, lines = result.Lines
            })
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<StoredPurchaseOrder> UpdateAsync(StoredPurchaseOrder expected, CreatePurchaseOrder request, int userId, string userName, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await LockAndCheck(db, expected, ct);
        var before = ToStored(order);
        if (!order.Status.Equals("DRAFT", StringComparison.OrdinalIgnoreCase))
            throw new PurchaseOrderConflictException("Chỉ được sửa PO ở trạng thái DRAFT.");
        if (order.Lines.Any(x => x.ClosedQuantity != 0))
            throw new PurchaseOrderConflictException("PO nháp có số lượng đóng không hợp lệ.");
        order.SupplierId = request.SupplierId;
        order.WarehouseId = request.WarehouseId;
        order.ExpectedArrivalDate = request.ExpectedArrivalDate;
        order.Note = request.Note;
        order.Status = "DRAFT";
        // Even a line-only edit must invalidate the header's row version.
        db.Entry(order).Property(x => x.Note).IsModified = true;
        foreach (var removed in order.Lines.Where(x => !request.Lines.Any(item => item.ProductId == x.ProductId)).ToArray())
        {
            db.PurchaseOrderLines.Remove(removed);
            order.Lines.Remove(removed);
        }
        foreach (var input in request.Lines)
        {
            var line = order.Lines.SingleOrDefault(x => x.ProductId == input.ProductId);
            if (line is null) order.Lines.Add(new LineEntity { ProductId = input.ProductId, OrderedQuantity = input.Quantity });
            else line.OrderedQuantity = input.Quantity;
        }
        await SaveOrder(db, ct);
        var after = ToStored(order);
        AddAudit(db, "UPDATE_PO", before, after, userId, userName);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return after;
    }

    public async Task<StoredPurchaseOrder> TransitionAsync(StoredPurchaseOrder expected, string targetStatus, string? reason,
        IReadOnlyDictionary<long, int> closedQuantities, int userId, string userName, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await LockAndCheck(db, expected, ct);
        var before = ToStored(order);
        var current = order.Status.ToUpperInvariant();
        var allowed = targetStatus switch { "CONFIRMED" => current == "DRAFT", "CANCELLED" => current is "DRAFT" or "CONFIRMED", "CLOSED" => current == "CONFIRMED", _ => false };
        if (!allowed) throw new PurchaseOrderConflictException("Trạng thái PO không cho phép thao tác này.");
        if (targetStatus is "CLOSED" or "CANCELLED")
        {
            if (await db.ReceivingReservations.AnyAsync(x => x.PurchaseOrderId == order.PurchaseOrderId
                    && (targetStatus == "CANCELLED" || x.Status == "OPEN"), ct))
                throw new PurchaseOrderConflictException(targetStatus == "CANCELLED"
                    ? "Không thể hủy PO đã có yêu cầu nhận hàng."
                    : "PO còn yêu cầu nhận hàng đang mở. Hoàn tất hoặc hủy phiếu nhập trước khi đóng PO.");
        }
        if (targetStatus == "CLOSED")
        {
            if (closedQuantities.Count != before.Lines.Length || before.Lines.Length == 0
                || before.Lines.Any(x => !closedQuantities.TryGetValue(x.PoLineId, out var value) || value < 0 || value > x.Quantity || x.ClosedQuantity != 0))
                throw new PurchaseOrderConflictException("Số lượng đóng không khớp các dòng PO.");
            foreach (var line in order.Lines) line.ClosedQuantity = closedQuantities[line.PoLineId];
        }
        order.Status = targetStatus;
        if (targetStatus == "CONFIRMED") order.ConfirmedAt = DateTime.UtcNow;
        if (targetStatus is "CANCELLED" or "CLOSED") { order.ClosedAt = DateTime.UtcNow; order.CloseReason = reason; }
        await SaveOrder(db, ct);
        var after = ToStored(order);
        var action = targetStatus switch { "CONFIRMED" => "CONFIRM_PO", "CANCELLED" => "CANCEL_PO", _ => "CLOSE_PO" };
        AddAudit(db, action, before, after, userId, userName);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return after;
    }

    private static async Task<OrderEntity> LockAndCheck(ProcurementDbContext db, StoredPurchaseOrder expected, CancellationToken ct)
    {
        // Reservations take this same row lock before changing the guard state.
        var order = await db.PurchaseOrders.FromSqlInterpolated($"SELECT * FROM dbo.PurchaseOrder WITH (UPDLOCK,HOLDLOCK) WHERE PurchaseOrderID = {expected.PurchaseOrderId}")
            .SingleOrDefaultAsync(ct) ?? throw new PurchaseOrderNotFoundException();
        if (Convert.ToBase64String(order.RowVersion) != expected.RowVersion)
            throw new PurchaseOrderConflictException("PO đã được thay đổi bởi thao tác khác. Vui lòng tải lại.");
        await db.Entry(order).Collection(x => x.Lines).LoadAsync(ct);
        return order;
    }

    private static async Task SaveOrder(ProcurementDbContext db, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new PurchaseOrderConflictException("PO đã được thay đổi bởi thao tác khác. Vui lòng tải lại."); }
    }

    private static StoredPurchaseOrder ToStored(OrderEntity order) => new(order.PurchaseOrderId, order.PoCode, order.SupplierId,
        order.WarehouseId, order.OrderDate, order.ExpectedArrivalDate, order.Status, DateTime.SpecifyKind(order.CreatedAt, DateTimeKind.Utc),
        order.CreatedByUserId, order.Note, order.Lines.OrderBy(x => x.PoLineId).Select(x => new StoredLine(x.PoLineId, x.ProductId, x.OrderedQuantity, x.ClosedQuantity)).ToArray(),
        Convert.ToBase64String(order.RowVersion), Utc(order.ConfirmedAt), Utc(order.ClosedAt), order.CloseReason);

    private static DateTime? Utc(DateTime? value) => value is { } date ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : null;

    private static void AddAudit(ProcurementDbContext db, string action, StoredPurchaseOrder before, StoredPurchaseOrder after, int userId, string name)
        => db.AuditLogs.Add(new AuditLog { UserId = userId, Action = action, EntityName = "PurchaseOrder",
            EntityId = after.PurchaseOrderId.ToString(CultureInfo.InvariantCulture), OldValues = Snapshot(before, null),
            NewValues = Snapshot(after, name), CreatedAt = DateTime.UtcNow });

    private static string Snapshot(StoredPurchaseOrder order, string? actor) => JsonSerializer.Serialize(new
    {
        purchaseOrderId = order.PurchaseOrderId, poCode = order.PoCode, order.SupplierId, order.WarehouseId,
        order.CreatedByUserId, order.OrderDate, order.ExpectedArrivalDate, order.Note,
        status = order.Status.ToUpperInvariant(), order.ConfirmedAt, order.ClosedAt, order.CloseReason,
        performedByName = actor, lines = order.Lines
    });
}