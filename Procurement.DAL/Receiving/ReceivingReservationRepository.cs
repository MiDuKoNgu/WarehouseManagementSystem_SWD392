using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Procurement.BLL.PurchaseOrders;
using Procurement.Models.Entities;

namespace Procurement.DAL.Receiving;

/// <summary>A durable guard between receipt creation and PO terminal transitions.</summary>
public sealed class ReceivingReservationRepository(IDbContextFactory<ProcurementDbContext> contexts)
{
    public ReceivingReservationRepository(string connectionString) : this(new ProcurementContextFactory(connectionString)) { }

    public async Task ReserveAsync(long purchaseOrderId, Guid token, int warehouseId, int userId, CancellationToken ct)
    {
        if (token == Guid.Empty) throw new PurchaseOrderValidationException(["Mã yêu cầu tạo phiếu không hợp lệ."]);
        await using var db = await contexts.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await LockOrder(db, purchaseOrderId, warehouseId, ct);
        var existing = await db.ReceivingReservations.SingleOrDefaultAsync(x => x.Token == token, ct);
        if (existing is not null)
        {
            if (existing.PurchaseOrderId != purchaseOrderId || existing.WarehouseId != warehouseId)
                throw new PurchaseOrderConflictException("Mã yêu cầu đã được sử dụng cho đơn mua hàng khác.");
            await transaction.CommitAsync(ct);
            return;
        }
        if (!order.Status.Equals("CONFIRMED", StringComparison.OrdinalIgnoreCase))
            throw new PurchaseOrderConflictException("Chỉ được tạo phiếu nhập từ PO đã xác nhận.");
        db.ReceivingReservations.Add(new ReceivingReservation
        {
            Token = token, PurchaseOrderId = purchaseOrderId, WarehouseId = warehouseId,
            CreatedByUserId = userId, Status = "OPEN", CreatedAt = DateTime.UtcNow
        });
        // Touching the header invalidates snapshots taken before this reservation.
        db.Entry(order).Property(x => x.Note).IsModified = true;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new PurchaseOrderConflictException("Mã yêu cầu đang được sử dụng. Vui lòng thử lại với cùng mã."); }
        await transaction.CommitAsync(ct);
    }

    // The caller obtains immutable terminal-receipt proof from Inventory before releasing.
    public async Task ReleaseAsync(long purchaseOrderId, Guid token, int warehouseId, CancellationToken ct)
    {
        if (token == Guid.Empty) throw new PurchaseOrderValidationException(["Mã yêu cầu không hợp lệ."]);
        await using var db = await contexts.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await LockOrder(db, purchaseOrderId, warehouseId, ct);
        var reservation = await db.ReceivingReservations.SingleOrDefaultAsync(x => x.Token == token
            && x.PurchaseOrderId == purchaseOrderId && x.WarehouseId == warehouseId, ct)
            ?? throw new PurchaseOrderConflictException("Không tìm thấy yêu cầu nhận hàng tương ứng.");
        if (reservation.Status == "OPEN")
        {
            reservation.Status = "RELEASED";
            reservation.ReleasedAt = DateTime.UtcNow;
            db.Entry(order).Property(x => x.Note).IsModified = true;
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private static async Task<PurchaseOrder> LockOrder(ProcurementDbContext db, long purchaseOrderId, int warehouseId, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.FromSqlInterpolated($"SELECT * FROM dbo.PurchaseOrder WITH (UPDLOCK,HOLDLOCK) WHERE PurchaseOrderID = {purchaseOrderId}")
            .SingleOrDefaultAsync(ct);
        if (order is null || order.WarehouseId != warehouseId) throw new PurchaseOrderNotFoundException();
        return order;
    }
}