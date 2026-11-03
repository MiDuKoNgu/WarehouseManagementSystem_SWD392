using Inventory.Models.Receiving;
using Microsoft.EntityFrameworkCore;

namespace Inventory.DAL.Receiving;

public interface IReceivingSummaryRepository
{ Task<PurchaseOrderReceivingSummary> GetAsync(long purchaseOrderId, CancellationToken ct); }

public sealed class ReceivingSummaryRepository(IDbContextFactory<InventoryDbContext> factory) : IReceivingSummaryRepository
{
    public ReceivingSummaryRepository(string connectionString) : this(new InventoryContextFactory(connectionString)) { }
    public async Task<PurchaseOrderReceivingSummary> GetAsync(long purchaseOrderId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var receipts = db.Inbounds.AsNoTracking().Where(x => x.PurchaseOrderId == purchaseOrderId);
        var any = await receipts.AnyAsync(ct);
        var open = await receipts.CountAsync(x => x.Status.ToUpper() != "CANCELLED" &&
            ((x.Status.ToUpper() != "DONE" && x.Status.ToUpper() != "COMPLETED") || x.CompletedAt == null), ct);
        var items = db.InboundItems.AsNoTracking().Where(x => x.Inbound.PurchaseOrderId == purchaseOrderId && x.IsActive &&
            (x.Inbound.Status.ToUpper() == "DONE" || x.Inbound.Status.ToUpper() == "COMPLETED") && x.Inbound.CompletedAt != null);
        var lines = await items.Where(x => x.SerialItem != null).GroupBy(x => x.POLineId)
            .Select(g => new ReceivingLine(g.Key, g.Select(x => x.SerialItemId).Distinct().Count())).ToArrayAsync(ct);
        var invalid = await items.AnyAsync(x => x.SerialItem == null, ct) ||
            await items.Where(x => x.SerialItemId != null).GroupBy(x => x.SerialItemId)
                .AnyAsync(g => g.Select(x => x.POLineId).Distinct().Count() > 1, ct);
        return new(purchaseOrderId, any, open, lines, invalid);
    }
}
