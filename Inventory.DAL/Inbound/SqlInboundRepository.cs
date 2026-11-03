using System.Text.Json;
using Inventory.BLL.Inbound;
using Inventory.Models.Inbound;
using Inventory.Models.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Receipt = Inventory.Models.Entities.Inbound;

namespace Inventory.DAL.Inbound;

public sealed class SqlInboundRepository(IDbContextFactory<InventoryDbContext> factory, ReceiptPolicy policy)
{
    public SqlInboundRepository(string connectionString, ReceiptPolicy policy) : this(new InventoryContextFactory(connectionString), policy) { }

    public async Task<ReceiptHeader[]> ListAsync(ReceiptActor actor, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.Inbounds.AsNoTracking().Where(x => x.WarehouseId == actor.WarehouseId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.InboundId)
            .Select(x => new { Receipt = x, Count = x.Items.Count(i => i.IsActive) }).ToArrayAsync(ct);
        return rows.Select(x => Header(x.Receipt, x.Count)).ToArray();
    }
    public async Task<ReceiptHeader?> FindAsync(long id, ReceiptActor actor, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Inbounds.AsNoTracking().Where(x => x.InboundId == id && x.WarehouseId == actor.WarehouseId)
            .Select(x => new { Receipt = x, Count = x.Items.Count(i => i.IsActive) }).SingleOrDefaultAsync(ct);
        return row is null ? null : Header(row.Receipt, row.Count);
    }
    public async Task<ReceiptHeader?> FindByTokenAsync(Guid token, ReceiptActor actor, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Inbounds.AsNoTracking().Where(x => x.CreationToken == token && x.WarehouseId == actor.WarehouseId)
            .Select(x => new { Receipt = x, Count = x.Items.Count(i => i.IsActive) }).SingleOrDefaultAsync(ct);
        return row is null ? null : Header(row.Receipt, row.Count);
    }
    public async Task<ReceiptItem[]> ItemsAsync(long id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return (await Items(db, id, ct)).Select(Item).ToArray();
    }
    public async Task<ReceiptProgress[]> ProgressAsync(ReceiptOrder order, long? currentId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await Progress(db, order, currentId, ct);
    }
    public async Task<long> CreateAsync(ReceiptOrder order, string? note, ReceiptActor actor, CancellationToken ct, Guid? creationToken = null)
    {
        CheckOrder(order, actor);
        if (note?.Length > 1000) throw new ReceiptException(400, "Ghi chú tối đa 1000 ký tự.");
        return await Write(async db =>
        {
            if (creationToken is { } token)
            {
                var existing = await db.Inbounds.SingleOrDefaultAsync(x => x.CreationToken == token, ct);
                if (existing is not null)
                {
                    if (existing.PurchaseOrderId != order.PurchaseOrderId || existing.WarehouseId != actor.WarehouseId)
                        throw new ReceiptException(409, "Mã yêu cầu đã thuộc phiếu nhập khác.");
                    if (existing.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase))
                        throw new ReceiptException(409, "Yêu cầu tạo phiếu đã bị hủy. Vui lòng bắt đầu yêu cầu mới.");
                    return existing.InboundId;
                }
            }
            if (!policy.AllowMultipleDrafts && await db.Inbounds.AnyAsync(x => x.PurchaseOrderId == order.PurchaseOrderId &&
                x.Status.ToUpper() != "DONE" && x.Status.ToUpper() != "COMPLETED" && x.Status.ToUpper() != "CANCELLED", ct))
                throw new ReceiptException(409, "PO đã có phiếu nhập đang mở. Vui lòng tiếp tục phiếu đó.");
            if (!(await Progress(db, order, null, ct)).Any(x => x.RemainingQuantity > 0))
                throw new ReceiptException(409, "PO không còn số lượng được nhận.");
            var row = new Receipt { PurchaseOrderId = order.PurchaseOrderId, CreatedByUserId = actor.UserId,
                CreatedByName = actor.Name, WarehouseId = actor.WarehouseId, CreationToken = creationToken,
                ReceiptCode = $"IN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..36], ReceiptDate = InboundRules.Today(),
                Status = "DRAFT", CreatedAt = DateTime.UtcNow, Note = note?.Trim() };
            db.Inbounds.Add(row);
            await db.SaveChangesAsync(ct);
            await Audit(db, row.InboundId, "INBOUND_CREATED", actor, null,
                new { inboundId = row.InboundId, order.PurchaseOrderId, receiptCode = row.ReceiptCode, status = "DRAFT", createdByName = actor.Name }, ct);
            return row.InboundId;
        }, ct);
    }
    public async Task<bool> AbortCreationAsync(ReceiptOrder order, ReceiptActor actor, Guid token, string reason, CancellationToken ct)
    {
        if (actor.WarehouseId <= 0 || order.WarehouseId != actor.WarehouseId) throw new ReceiptException(403, "PO không thuộc kho được phân công.");
        return await Write(async db =>
        {
            var existing = await db.Inbounds.SingleOrDefaultAsync(x => x.CreationToken == token, ct);
            if (existing is not null)
            {
                if (existing.PurchaseOrderId != order.PurchaseOrderId || existing.WarehouseId != actor.WarehouseId)
                    throw new ReceiptException(409, "Mã yêu cầu đã thuộc phiếu nhập khác.");
                return existing.Status.ToUpperInvariant() is "CANCELLED" or "DONE" or "COMPLETED";
            }
            var note = "Tạo phiếu thất bại: " + reason;
            if (note.Length > 1000) note = note[..1000];
            var row = new Receipt { PurchaseOrderId = order.PurchaseOrderId, CreatedByUserId = actor.UserId,
                CreatedByName = actor.Name, PerformedByUserId = actor.UserId, PerformedByName = actor.Name,
                WarehouseId = actor.WarehouseId, CreationToken = token, ReceiptCode = $"IN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..36],
                ReceiptDate = InboundRules.Today(), Status = "CANCELLED", CreatedAt = DateTime.UtcNow, Note = note };
            db.Inbounds.Add(row); await db.SaveChangesAsync(ct);
            await Audit(db, row.InboundId, "INBOUND_CANCELLED", actor, null,
                new { inboundId = row.InboundId, order.PurchaseOrderId, receiptCode = row.ReceiptCode, status = "CANCELLED", reason = note, creationToken = token, creationFailed = true }, ct);
            return true;
        }, ct);
    }
    public async Task SaveItemAsync(long id, long? itemId, SaveReceiptItem request, ReceiptOrder order, ReceiptActor actor, CancellationToken ct)
    {
        request = InboundRules.Validate(request, policy, InboundRules.Today()); CheckOrder(order, actor);
        await Write(async db =>
        {
            var header = await Draft(db, id, request.RowVersion, actor, ct);
            if (header.PurchaseOrderId != order.PurchaseOrderId) throw new ReceiptException(409, "PO không khớp phiếu nhập.");
            if (!order.Lines.Any(x => x.PoLineId == request.PoLineId)) throw new ReceiptException(400, "Dòng sản phẩm không thuộc PO.");
            var items = await Items(db, id, ct);
            var row = itemId is null ? null : items.SingleOrDefault(x => x.InboundItemId == itemId);
            if (itemId is not null && row is null) throw new ReceiptException(404, "Không tìm thấy Serial đang hoạt động.");
            var before = row is null ? null : Item(row);
            await CheckSerial(db, request.Serial, itemId, ct);
            var line = (await Progress(db, order, id, ct)).Single(x => x.PoLineId == request.PoLineId);
            if (line.RemainingQuantity + (before?.PoLineId == request.PoLineId ? 1 : 0) <= 0)
                throw new ReceiptException(409, "Dòng PO đã đủ số lượng hoặc được giữ trong phiếu khác.");
            if (row is null)
            {
                row = new InboundItem { InboundId = id, ScannedAt = DateTime.UtcNow, IsActive = true,
                    ScannedByUserId = actor.UserId, ScannedByName = actor.Name };
                db.InboundItems.Add(row);
            }
            row.POLineId = request.PoLineId; row.ScannedSerial = request.Serial;
            row.ManufacturingMonth = checked((byte)request.ManufacturingMonth); row.ManufacturingYear = checked((short)request.ManufacturingYear);
            row.ReceivedCondition = request.ReceivedCondition; row.ConditionNote = request.ConditionNote; row.InputMethod = request.InputMethod;
            Touch(db, header); await db.SaveChangesAsync(ct);
            await Audit(db, id, itemId is null ? "INBOUND_SERIAL_ADDED" : "INBOUND_SERIAL_UPDATED", actor, before,
                new { inboundId = id, inboundItemId = row.InboundItemId, request.PoLineId, serial = request.Serial, request.ManufacturingMonth,
                    request.ManufacturingYear, request.ReceivedCondition, request.ConditionNote, request.InputMethod }, ct);
            return true;
        }, ct);
    }
    public async Task RemoveItemAsync(long id, long itemId, string version, ReceiptActor actor, CancellationToken ct)
    {
        await Write(async db =>
        {
            var header = await Draft(db, id, version, actor, ct);
            var row = (await Items(db, id, ct)).SingleOrDefault(x => x.InboundItemId == itemId)
                ?? throw new ReceiptException(404, "Không tìm thấy Serial đang hoạt động.");
            var before = Item(row); row.IsActive = false; Touch(db, header); await db.SaveChangesAsync(ct);
            await Audit(db, id, "INBOUND_SERIAL_REMOVED", actor, before, new { inboundId = id, inboundItemId = itemId, serial = row.ScannedSerial, isActive = false }, ct);
            return true;
        }, ct);
    }
    public async Task CompleteAsync(long id, string version, ReceiptOrder order, ReceiptActor actor, CancellationToken ct)
    {
        CheckOrder(order, actor);
        await Write(async db =>
        {
            var header = await Draft(db, id, version, actor, ct);
            if (header.PurchaseOrderId != order.PurchaseOrderId) throw new ReceiptException(409, "PO không khớp phiếu nhập.");
            var items = await Items(db, id, ct);
            var before = Header(header, items.Length);
            if (items.Length == 0) throw new ReceiptException(400, "Cần ít nhất một Serial để hoàn tất phiếu.");
            foreach (var item in items)
            {
                InboundRules.Validate(new(item.POLineId, item.ScannedSerial, item.ManufacturingMonth ?? 0, item.ManufacturingYear ?? 0,
                    item.ReceivedCondition ?? "", item.ConditionNote, item.InputMethod, version), policy, InboundRules.Today());
                if (item.SerialItemId is not null || !order.Lines.Any(x => x.PoLineId == item.POLineId))
                    throw new ReceiptException(409, "Serial hoặc dòng PO không hợp lệ để hoàn tất.");
                await CheckSerial(db, item.ScannedSerial, item.InboundItemId, ct);
            }
            await Progress(db, order, id, ct);
            foreach (var group in items.GroupBy(x => new { ProductId = order.Lines.Single(l => l.PoLineId == x.POLineId).ProductId, Condition = x.ReceivedCondition! }))
            {
                var source = new SupplySource { ProductId = group.Key.ProductId, WarehouseId = actor.WarehouseId, ItemCondition = group.Key.Condition,
                    SourceType = "INBOUND", SupplyState = "PHYSICAL", InboundId = id, EligibleQuantity = group.Key.Condition == "DAMAGED" ? 0 : group.Count(),
                    RemainingQuantity = group.Count(), AvailableFrom = InboundRules.Today(), CreatedAt = DateTime.UtcNow };
                db.SupplySources.Add(source); await db.SaveChangesAsync(ct);
                foreach (var item in group)
                {
                    var serial = new SerialItem { ProductId = group.Key.ProductId, SerialNumber = item.ScannedSerial,
                        ManufacturingMonth = item.ManufacturingMonth, ManufacturingYear = item.ManufacturingYear, OriginType = "PURCHASE",
                        OriginalInboundId = id, CurrentWarehouseId = actor.WarehouseId, CurrentSupplySourceId = source.SupplySourceId,
                        StockState = "IN_WAREHOUSE", ItemCondition = item.ReceivedCondition!, IsQuarantined = item.ReceivedCondition == "DAMAGED",
                        QuarantineReason = item.ReceivedCondition == "DAMAGED" ? item.ConditionNote : null, RegisteredAt = DateTime.UtcNow };
                    db.SerialItems.Add(serial); await db.SaveChangesAsync(ct);
                    item.SerialItemId = serial.SerialItemId;
                    db.SerialMovements.Add(new SerialMovement { SerialItemId = serial.SerialItemId, PerformedByUserId = actor.UserId,
                        EventType = "PURCHASE_RECEIPT", OccurredAt = DateTime.UtcNow, ToState = "IN_WAREHOUSE", ToCondition = serial.ItemCondition,
                        ToWarehouseId = actor.WarehouseId, ToQuarantined = serial.IsQuarantined, InboundId = id, Note = "Chờ Putaway" });
                }
            }
            header.Status = "DONE"; header.CompletedAt = DateTime.UtcNow; header.PerformedByUserId = actor.UserId; header.PerformedByName = actor.Name;
            await db.SaveChangesAsync(ct);
            await Audit(db, id, "INBOUND_COMPLETED", actor, before, new { inboundId = id, status = "DONE", totalSerials = items.Length, pendingPutaway = true }, ct);
            return true;
        }, ct);
    }
    public async Task CancelAsync(long id, string version, string reason, ReceiptActor actor, CancellationToken ct)
    {
        if (!policy.AllowCancellation) throw new ReceiptException(403, "Chính sách hiện tại không cho hủy toàn phiếu.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000) throw new ReceiptException(400, "Lý do hủy cần từ 1 đến 1000 ký tự.");
        await Write(async db =>
        {
            var header = await Draft(db, id, version, actor, ct);
            var items = await Items(db, id, ct); var before = Header(header, items.Length);
            foreach (var item in items) item.IsActive = false;
            header.Status = "CANCELLED"; header.PerformedByUserId = actor.UserId; header.PerformedByName = actor.Name;
            await db.SaveChangesAsync(ct);
            await Audit(db, id, "INBOUND_CANCELLED", actor, before, new { inboundId = id, status = "CANCELLED", reason = reason.Trim() }, ct);
            return true;
        }, ct);
    }
    private async Task<T> Write<T>(Func<InventoryDbContext, Task<T>> action, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var locks = await db.Database.SqlQueryRaw<int>("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource='EWMS:InboundWrite',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; SELECT @result AS Value").ToArrayAsync(ct);
        if (locks.Single() < 0) throw new ReceiptException(409, "Kho đang xử lý yêu cầu khác. Vui lòng thử lại.");
        try { var result = await action(db); await transaction.CommitAsync(ct); return result; }
        catch (DbUpdateConcurrencyException) { throw new ReceiptException(409, "Phiếu đã thay đổi. Vui lòng tải lại trước khi thao tác."); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new ReceiptException(409, "Serial đã tồn tại hoặc vừa được nhận trong yêu cầu khác."); }
    }
    private static async Task<Receipt> Draft(InventoryDbContext db, long id, string version, ReceiptActor actor, CancellationToken ct)
    {
        InboundRules.Version(version);
        var row = await db.Inbounds.FromSqlInterpolated($"SELECT * FROM dbo.Inbound WITH (UPDLOCK,HOLDLOCK) WHERE InboundID={id} AND WarehouseID={actor.WarehouseId}").SingleOrDefaultAsync(ct)
            ?? throw new ReceiptException(404, "Không tìm thấy phiếu nhập trong kho của bạn.");
        if (!row.Status.Equals("DRAFT", StringComparison.OrdinalIgnoreCase)) throw new ReceiptException(409, "Chỉ được thao tác phiếu DRAFT.");
        if (Convert.ToBase64String(row.RowVersion) != version) throw new ReceiptException(409, "Phiếu đã thay đổi. Vui lòng tải lại trước khi thao tác.");
        return row;
    }
    private static Task<InboundItem[]> Items(InventoryDbContext db, long id, CancellationToken ct) => db.InboundItems
        .Where(x => x.InboundId == id && x.IsActive).OrderByDescending(x => x.ScannedAt).ThenByDescending(x => x.InboundItemId).ToArrayAsync(ct);
    private static ReceiptHeader Header(Receipt x, int count) => new(x.InboundId, x.PurchaseOrderId, x.ReceiptCode,
        x.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase) ? "DONE" : x.Status.ToUpperInvariant(), x.ReceiptDate, Utc(x.CreatedAt),
        x.CreatedByUserId, x.CreatedByName, x.CompletedAt is { } at ? Utc(at) : null, x.PerformedByUserId, x.PerformedByName, x.Note,
        Convert.ToBase64String(x.RowVersion), x.WarehouseId ?? 0, count);
    private static ReceiptItem Item(InboundItem x) => new(x.InboundItemId, x.POLineId, x.ScannedSerial, x.ManufacturingMonth, x.ManufacturingYear,
        x.ReceivedCondition, x.ConditionNote, x.InputMethod, x.ScannedByUserId, x.ScannedByName, Utc(x.ScannedAt), x.SerialItemId);
    private static void Touch(InventoryDbContext db, Receipt header) => db.Entry(header).Property(x => x.Note).IsModified = true;
    private static async Task CheckSerial(InventoryDbContext db, string serial, long? excluded, CancellationToken ct)
    {
        if (await db.SerialItems.AnyAsync(x => x.SerialNumber == serial, ct) ||
            await db.InboundItems.AnyAsync(x => x.ScannedSerial == serial && x.IsActive && (excluded == null || x.InboundItemId != excluded), ct))
            throw new ReceiptException(409, "Serial đã tồn tại hoặc đang được giữ trong phiếu nhập khác.");
    }
    private static async Task Audit(InventoryDbContext db, long id, string action, ReceiptActor actor, object? before, object after, CancellationToken ct)
    {
        var values = JsonSerializer.SerializeToElement(after).EnumerateObject().ToDictionary(x => x.Name, x => (object?)x.Value);
        values["inboundId"] = id; values["performedByName"] = actor.Name; values["performedByUserId"] = actor.UserId;
        var oldJson = before is null ? null : JsonSerializer.Serialize(before); var newJson = JsonSerializer.Serialize(values);
        var entityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.AuditLog(UserID,Action,EntityName,EntityID,OldValues,NewValues,Status,CreatedAt) VALUES({actor.UserId},{action},'Inbound',{entityId},{oldJson},{newJson},'SUCCESS',SYSUTCDATETIME())", ct);
    }
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static void CheckOrder(ReceiptOrder order,ReceiptActor actor)
    {
        if(actor.WarehouseId<=0||order.WarehouseId!=actor.WarehouseId) throw new ReceiptException(403,"PO không thuộc kho được phân công.");
        if(!order.Status.Equals("CONFIRMED",StringComparison.OrdinalIgnoreCase)||order.Lines.Length==0)
            throw new ReceiptException(409,"Chỉ được nhận hàng từ PO đã CONFIRMED có dòng sản phẩm.");
        if(order.Lines.Any(line=>line.ProductId<=0||line.PoLineId<=0||line.OrderedQuantity<=0||line.ClosedQuantity<0||line.ClosedQuantity>line.OrderedQuantity)
            ||order.Lines.Select(line=>line.PoLineId).Distinct().Count()!=order.Lines.Length)
            throw new ReceiptException(409,"Dữ liệu dòng PO không nhất quán.");
    }

    private static async Task<ReceiptProgress[]> Progress(InventoryDbContext db, ReceiptOrder order, long? current, CancellationToken ct)
    {
        var rows = await db.InboundItems.AsNoTracking()
            .Where(x => x.IsActive && x.Inbound.PurchaseOrderId == order.PurchaseOrderId && x.Inbound.Status.ToUpper() != "CANCELLED")
            .GroupBy(x => x.POLineId)
            .Select(g => new {
                Line = g.Key,
                Received = g.Where(x => (x.Inbound.Status.ToUpper() == "DONE" || x.Inbound.Status.ToUpper() == "COMPLETED") && x.Inbound.CompletedAt != null && x.SerialItem != null).Select(x => x.SerialItemId).Distinct().Count(),
                Reserved = g.Count(x => x.Inbound.Status.ToUpper() == "DRAFT" && (current == null || x.InboundId != current)),
                // When current is null, EF folds a filtered Count into COUNT(NULL),
                // which SQL Server rejects. SUM(0) keeps the no-current-receipt case valid.
                Current = g.Sum(x => x.InboundId == current ? 1 : 0),
                Draft = g.Sum(x => x.InboundId == current && x.Inbound.Status.ToUpper() == "DRAFT" ? 1 : 0),
                Invalid = g.Count(x => (x.Inbound.Status.ToUpper() == "DONE" || x.Inbound.Status.ToUpper() == "COMPLETED") && (x.Inbound.CompletedAt == null || x.SerialItem == null))
            }).ToArrayAsync(ct);
        if (rows.Any(x => x.Invalid > 0 || !order.Lines.Any(l => l.PoLineId == x.Line)))
            throw new ReceiptException(409, "Dữ liệu nhận hàng lịch sử không khớp PO. Vui lòng kiểm tra.");
        return order.Lines.Select(line =>
        {
            var count = rows.SingleOrDefault(x => x.Line == line.PoLineId);
            var received = count?.Received ?? 0; var reserved = count?.Reserved ?? 0; var draft = count?.Draft ?? 0;
            var remaining = (long)line.OrderedQuantity - line.ClosedQuantity - received - reserved - draft;
            if (remaining < 0) throw new ReceiptException(409, "Tổng số lượng nhận và giữ chỗ đã vượt PO.");
            return new ReceiptProgress(line.PoLineId, line.ProductId, line.ProductCode, line.ProductName, line.OrderedQuantity,
                line.ClosedQuantity, received, reserved, count?.Current ?? 0, (int)remaining);
        }).ToArray();
    }
}
