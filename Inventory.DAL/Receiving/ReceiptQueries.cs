using Microsoft.EntityFrameworkCore;

namespace Inventory.DAL.Receiving;

public sealed class ReceiptQueries(IDbContextFactory<InventoryDbContext> factory)
{
    public ReceiptQueries(string connectionString) : this(new InventoryContextFactory(connectionString)) { }
    public async Task<ReservationProof> ReservationAsync(Guid token, int warehouseId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Inbounds.AsNoTracking().Where(x => x.CreationToken == token && x.WarehouseId == warehouseId)
            .Select(x => new { x.PurchaseOrderId, x.Status }).SingleOrDefaultAsync(ct);
        return row is null ? new(false, false, 0, warehouseId) : new(true, row.Status.ToUpperInvariant() is "DONE" or "COMPLETED" or "CANCELLED", row.PurchaseOrderId, warehouseId);
    }
    public async Task<Guid?> TokenAsync(long id, int warehouseId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Inbounds.Where(x => x.InboundId == id && x.WarehouseId == warehouseId).Select(x => x.CreationToken).SingleOrDefaultAsync(ct);
    }
    public async Task<PendingSerial[]> PendingAsync(int warehouseId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await (from s in db.SerialItems.AsNoTracking()
            join r in db.Inbounds on s.OriginalInboundId equals r.InboundId into receipts
            from r in receipts.DefaultIfEmpty()
            where s.CurrentWarehouseId == warehouseId && s.StockState == "IN_WAREHOUSE" && s.CurrentLocationId == null &&
                ((s.OriginalInboundId == null && s.OriginType != "PURCHASE") || ((r.Status.ToUpper() == "DONE" || r.Status.ToUpper() == "COMPLETED") && r.CompletedAt != null))
            orderby s.RegisteredAt, s.SerialItemId
            select new PendingSerial(s.SerialItemId, s.SerialNumber, s.ProductId, s.OriginalInboundId, r == null ? null : r.ReceiptCode,
                s.ManufacturingMonth, s.ManufacturingYear, s.ItemCondition, s.IsQuarantined, s.RegisteredAt)).ToArrayAsync(ct);
        return rows.Select(x => x with { RegisteredAt = DateTime.SpecifyKind(x.RegisteredAt, DateTimeKind.Utc) }).ToArray();
    }
}
