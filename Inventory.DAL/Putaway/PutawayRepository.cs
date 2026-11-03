using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Inventory.BLL.Putaway;
using Inventory.Models.Entities;
using Inventory.Models.Putaway;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Inventory.DAL.Putaway;

/// <summary>Inventory owns writes. Configuration reads retain same-instance transaction locks without cross-domain writes.</summary>
public sealed class PutawayRepository : IPutawayRepository
{
    private readonly IDbContextFactory<InventoryDbContext> contexts;
    private readonly string catalog;
    private readonly string warehouse;
    private readonly IPutawayOperationGuard guard;

    public PutawayRepository(string connectionString, IPutawayOperationGuard guard,
        string catalogDatabase = "CatalogDB", string warehouseDatabase = "WarehouseDB")
        : this(new InventoryContextFactory(connectionString), guard, catalogDatabase, warehouseDatabase) { }

    public PutawayRepository(IDbContextFactory<InventoryDbContext> contexts, IPutawayOperationGuard guard,
        string catalogDatabase = "CatalogDB", string warehouseDatabase = "WarehouseDB")
    {
        this.contexts = contexts; this.guard = guard;
        catalog = Database(catalogDatabase); warehouse = Database(warehouseDatabase);
    }

    private static string Database(string value) => Regex.IsMatch(value, @"\A[A-Za-z0-9_]{1,128}\z")
        ? "[" + value + "]" : throw new ArgumentException("Invalid Putaway database configuration.");
    private string ProfileColumns => """
        p.ProductID AS ProfileProductId,p.ProductCode,p.ProductName,p.CategoryID,c.CategoryName,p.BrandID,b.BrandName,
        CASE WHEN c.CategoryID IS NULL OR b.BrandID IS NULL THEN 'MISSING' ELSE p.Status END AS ProductStatus,
        p.LengthCm,p.WidthCm,p.HeightCm,p.GrossWeightKg
        """;
    private string ProductJoin(string hint) => $"LEFT JOIN {catalog}.dbo.Product p {hint} ON p.ProductID=s.ProductID LEFT JOIN {catalog}.dbo.Category c {hint} ON c.CategoryID=p.CategoryID LEFT JOIN {catalog}.dbo.Brand b {hint} ON b.BrandID=p.BrandID";
    private string SerialSql(bool locked) => $"""
        SELECT s.SerialItemID,s.SerialNumber,s.ProductID,s.StockState,s.ItemCondition,s.IsQuarantined,
            s.CurrentWarehouseID,s.CurrentLocationID,s.RowVersion,s.OriginalInboundID,i.ReceiptCode,s.RegisteredAt,
            CASE WHEN i.InboundID IS NULL THEN NULL WHEN UPPER(i.Status) IN ('DONE','COMPLETED') AND i.CompletedAt IS NOT NULL THEN 'DONE' ELSE 'NOT_DONE' END AS InboundStatus,
            s.OriginType,w.WarehouseName,{ProfileColumns}
        FROM dbo.SerialItem s {(locked ? "WITH (UPDLOCK,HOLDLOCK)" : "")}
        LEFT JOIN dbo.Inbound i {(locked ? "WITH (HOLDLOCK)" : "")} ON i.InboundID=s.OriginalInboundID
        LEFT JOIN {warehouse}.dbo.Warehouse w {(locked ? "WITH (HOLDLOCK)" : "")} ON w.WarehouseID=s.CurrentWarehouseID
        {ProductJoin(locked ? "WITH (HOLDLOCK)" : "")}
        """;

    public async Task<PutawayPendingPage> PendingAsync(PutawayActor actor, string? search, int page, int pageSize, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        const string filter = """
            WHERE s.CurrentWarehouseID=@warehouse AND s.StockState='IN_WAREHOUSE' AND s.CurrentLocationID IS NULL
                AND ((s.OriginalInboundID IS NULL AND s.OriginType<>'PURCHASE') OR (UPPER(i.Status) IN ('DONE','COMPLETED') AND i.CompletedAt IS NOT NULL))
                AND (@search IS NULL OR s.SerialNumber LIKE @search ESCAPE '~' OR p.ProductCode LIKE @search ESCAPE '~' OR p.ProductName LIKE @search ESCAPE '~' OR i.ReceiptCode LIKE @search ESCAPE '~')
            """;
        object[] Filters() => [P("@warehouse", SqlDbType.Int, actor.WarehouseId),
            P("@search", SqlDbType.NVarChar, string.IsNullOrEmpty(search) ? null : "%" + Escape(search) + "%", 402)];
        var parameters = Filters().Concat(new object[] { P("@skip", SqlDbType.Int, checked((page - 1) * pageSize)), P("@take", SqlDbType.Int, pageSize) }).ToArray();
        var rows = await db.Database.SqlQueryRaw<PutawaySerialRow>(SerialSql(false) + "\n" + filter +
            " ORDER BY s.RegisteredAt,s.SerialItemID OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY", parameters).ToArrayAsync(ct);
        var total = await db.Database.SqlQueryRaw<int>(
            $"SELECT COUNT(*) AS Value FROM dbo.SerialItem s LEFT JOIN dbo.Inbound i ON i.InboundID=s.OriginalInboundID {ProductJoin("")} " + filter,
            Filters()).ToArrayAsync(ct);
        return new(rows.Select(ReadSerial).ToArray(), total.Single(), page, pageSize);
    }

    public async Task<PutawaySerial> FindAsync(PutawayActor actor, long? id, string? serial, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var item = await ReadItem(db, actor, id, serial, ct);
        if (item.CurrentLocationId is { } location)
            item = item with { CurrentLocationPath = Path(await Snapshot(db, actor.WarehouseId, ct), location) };
        return item;
    }

    public async Task<PutawaySuggestions> SuggestionsAsync(PutawayActor actor, long id, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        await AssertSupportedControls(db, ct);
        var item = await ReadItem(db, actor, id, null, ct);
        var snapshot = await Snapshot(db, actor.WarehouseId, ct);
        item = item with { CurrentLocationPath = item.CurrentLocationId is { } location ? Path(snapshot, location) : null };
        PutawayRules.ValidateSerial(item, actor.WarehouseId);
        await guard.ValidateAsync(item, snapshot, ct);
        return PutawayRules.Suggest(item, snapshot, await Occupants(db, actor.WarehouseId, null, ct));
    }

    public async Task<PutawayResult> ConfirmAsync(PutawayActor actor, ConfirmPutaway request, CancellationToken ct)
    {
        var version = PutawayRules.Version(request.RowVersion);
        await using var db = await contexts.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT 10000;", ct);
            // Target first: a waiter must not hold a serial lock needed by the winner's occupancy scan.
            await Lock(db, $"EWMS:Inventory:Location:{actor.WarehouseId}:{request.TargetLocationId}", ct);
            await AssertSupportedControls(db, ct);
            var item = await ReadItem(db, actor, request.SerialItemId, null, ct);
            var snapshot = await Snapshot(db, actor.WarehouseId, ct);
            item = item with { CurrentLocationPath = item.CurrentLocationId is { } current ? Path(snapshot, current) : null };
            PutawayRules.ValidateSerial(item, actor.WarehouseId);
            if (!Convert.FromBase64String(item.RowVersion).AsSpan().SequenceEqual(version))
                throw new PutawayException(409, "STALE_SERIAL", "Serial đã thay đổi. Hãy tải lại thông tin trước khi xác nhận.");
            await guard.ValidateAsync(item, snapshot, ct);
            var target = snapshot.Locations.SingleOrDefault(node => node.LocationId == request.TargetLocationId)
                ?? throw new PutawayException(404, "LOCATION_NOT_FOUND", "Không tìm thấy vị trí trong kho được phân công.");
            var occupants = await Occupants(db, actor.WarehouseId, request.TargetLocationId, ct);
            var candidate = PutawayRules.Evaluate(item, snapshot, target, occupants);
            if (!candidate.IsValid)
                throw new PutawayException(409, "LOCATION_UNAVAILABLE", candidate.Reason ?? "Vị trí không còn hợp lệ.");
            var occurred = DateTime.UtcNow;

            // The locked projection above validates warehouse/state/location. Attach only the changed
            // entity property; EF includes its original RowVersion in the UPDATE and reads the new one.
            var entity = new SerialItem { SerialItemId = item.SerialItemId, RowVersion = version };
            db.SerialItems.Attach(entity);
            entity.CurrentLocationId = target.LocationId;
            db.Entry(entity).Property(x => x.CurrentLocationId).IsModified = true;
            await db.SaveChangesAsync(ct);
            var newVersion = Convert.ToBase64String(entity.RowVersion);

            db.SerialMovements.Add(new SerialMovement
            {
                SerialItemId = item.SerialItemId, PerformedByUserId = actor.UserId, EventType = "PUTAWAY",
                OccurredAt = occurred, FromState = "IN_WAREHOUSE", ToState = "IN_WAREHOUSE",
                FromCondition = item.ItemCondition, ToCondition = item.ItemCondition,
                FromWarehouseId = actor.WarehouseId, ToWarehouseId = actor.WarehouseId,
                FromLocationId = null, ToLocationId = target.LocationId,
                FromQuarantined = item.IsQuarantined, ToQuarantined = item.IsQuarantined,
                Note = $"Putaway: {candidate.LocationPath}; {actor.Name}"
            });
            await db.SaveChangesAsync(ct);

            var entityId = item.SerialItemId.ToString(CultureInfo.InvariantCulture);
            var before = JsonSerializer.Serialize(new { item.SerialItemId, currentLocationId = (int?)null, item.StockState, item.ItemCondition, item.IsQuarantined, item.RowVersion });
            var after = JsonSerializer.Serialize(new { item.SerialItemId, item.SerialNumber, currentLocationId = target.LocationId,
                locationPath = candidate.LocationPath, warehouseId = actor.WarehouseId, item.StockState, item.ItemCondition,
                item.IsQuarantined, inboundId = item.OriginalInboundId, performedByUserId = actor.UserId,
                performedByName = actor.Name, rowVersion = newVersion });
            // Audit key names differ in deployed fixtures; insert common columns in the same EF transaction.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.AuditLog(UserID,Action,EntityName,EntityID,OldValues,NewValues,Status,CreatedAt)
                VALUES({actor.UserId},'PUTAWAY_COMPLETED','Putaway',{entityId},{before},{after},'SUCCESS',{occurred})
                """, ct);
            await transaction.CommitAsync(ct);
            return new(item.SerialItemId, item.SerialNumber, target.LocationId, candidate.LocationPath, occurred, actor.UserId, actor.Name, newVersion);
        }
        catch (DbUpdateConcurrencyException)
        { throw new PutawayException(409, "STALE_SERIAL", "Serial đã thay đổi hoặc đã được Putaway."); }
        catch (Exception error) when (error is SqlException { Number: 1205 or 1222 } ||
            error is DbUpdateException { InnerException: SqlException { Number: 1205 or 1222 } })
        { throw new PutawayException(409, "CONCURRENT_PUTAWAY", "Vị trí hoặc Serial đang được xử lý. Hãy tải lại gợi ý và thử lại."); }
        // Dispose rolls back all writes, including movement/audit errors and cancellation.
    }

    private async Task<PutawaySerial> ReadItem(InventoryDbContext db, PutawayActor actor, long? id, string? serial, CancellationToken ct)
    {
        var rows = await db.Database.SqlQueryRaw<PutawaySerialRow>(SerialSql(db.Database.CurrentTransaction is not null) +
            " WHERE s.CurrentWarehouseID=@warehouse AND " + (id is not null ? "s.SerialItemID=@id" : "s.SerialNumber=@serial"),
            P("@warehouse", SqlDbType.Int, actor.WarehouseId), P("@id", SqlDbType.BigInt, id), P("@serial", SqlDbType.NVarChar, serial, 100)).ToArrayAsync(ct);
        var row = rows.FirstOrDefault()
            ?? throw new PutawayException(404, "SERIAL_NOT_FOUND", "Không tìm thấy Serial trong kho được phân công (hoặc hàng đã rời kho).");
        return ReadSerial(row);
    }

    // Only Database()-validated, quoted identifiers and constant lock hints are interpolated;
    // every request value is a SqlParameter. Identifiers cannot be SQL parameters.
#pragma warning disable EF1002
    private async Task<PutawayWarehouseSnapshot> Snapshot(InventoryDbContext db, int warehouseId, CancellationToken ct)
    {
        var hint = db.Database.CurrentTransaction is null ? "" : "WITH (HOLDLOCK)";
        var warehouses = await db.Database.SqlQueryRaw<PutawayWarehouseRow>(
            $"SELECT WarehouseName,Status,WarehouseType,RentalEndDate FROM {warehouse}.dbo.Warehouse {hint} WHERE WarehouseID=@warehouse",
            P("@warehouse", SqlDbType.Int, warehouseId)).ToArrayAsync(ct);
        var row = warehouses.SingleOrDefault() ?? throw new PutawayException(404, "WAREHOUSE_NOT_FOUND", "Kho được phân công không tồn tại.");
        var locations = await db.Database.SqlQueryRaw<PutawayLocationNode>($"""
            SELECT LocationID,WarehouseID,ParentLocationID,LocationName,LocationType,StorageType,CapacityMode,ConditionPolicy,ZoneType,Status,LengthCm,WidthCm,HeightCm,MaxWeightKg
            FROM {warehouse}.dbo.StorageLocation {hint} WHERE WarehouseID=@warehouse ORDER BY LocationID
            """, P("@warehouse", SqlDbType.Int, warehouseId)).ToArrayAsync(ct);
        var rules = await db.Database.SqlQueryRaw<PutawayZoneRule>($"""
            SELECT r.ZoneLocationID,r.CategoryID,r.BrandID FROM {warehouse}.dbo.ZoneStorageRule r {hint}
            JOIN {warehouse}.dbo.StorageLocation z {hint} ON z.LocationID=r.ZoneLocationID WHERE z.WarehouseID=@warehouse
            """, P("@warehouse", SqlDbType.Int, warehouseId)).ToArrayAsync(ct);
        return new(warehouseId, row.WarehouseName, row.Status, locations, rules, row.WarehouseType, row.RentalEndDate);
    }

    private async Task<PutawayOccupant[]> Occupants(InventoryDbContext db, int warehouseId, int? location, CancellationToken ct)
    {
        var hint = db.Database.CurrentTransaction is null ? "" : "WITH (HOLDLOCK)";
        var rows = await db.Database.SqlQueryRaw<PutawayOccupantRow>($"""
            SELECT s.SerialItemID,s.CurrentLocationID,s.ProductID,s.ItemCondition,{ProfileColumns}
            FROM dbo.SerialItem s {hint} {ProductJoin(hint)}
            WHERE s.CurrentWarehouseID=@warehouse AND s.StockState='IN_WAREHOUSE' AND s.CurrentLocationID IS NOT NULL
                AND (@location IS NULL OR s.CurrentLocationID=@location)
            """, P("@warehouse", SqlDbType.Int, warehouseId), P("@location", SqlDbType.Int, location)).ToArrayAsync(ct);
        return rows.Select(x => new PutawayOccupant(x.SerialItemID, x.CurrentLocationID, x.ProductID, x.ItemCondition,
            new(x.ProfileProductId ?? 0, x.ProductCode ?? "", x.ProductName ?? "Thiếu hồ sơ sản phẩm",
                x.CategoryID ?? 0, x.CategoryName ?? "", x.BrandID ?? 0, x.BrandName ?? "", x.ProductStatus ?? "MISSING",
                x.LengthCm, x.WidthCm, x.HeightCm, x.GrossWeightKg))).ToArray();
    }

#pragma warning restore EF1002
    private static async Task AssertSupportedControls(InventoryDbContext db, CancellationToken ct)
    {
        var count = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sys.tables WHERE name LIKE '%Stocktake%' OR name LIKE '%SerialClaim%' OR name LIKE '%PickingSession%' OR name LIKE '%PickingClaim%'").ToArrayAsync(ct);
        if (count.Single() > 0)
            throw new PutawayException(503, "MOVEMENT_GUARD_REQUIRED", "Schema đã có kiểm kê/claim. Cần tích hợp bộ kiểm soát di chuyển trước khi Putaway.");
    }

    private static async Task Lock(InventoryDbContext db, string resource, CancellationToken ct)
    {
        var results = await db.Database.SqlQueryRaw<int>("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; SELECT @result AS Value;",
            P("@resource", SqlDbType.NVarChar, resource, 255)).ToArrayAsync(ct);
        if (results.Single() < 0) throw new PutawayException(409, "LOCATION_BUSY", "Vị trí đang được xử lý. Hãy thử lại.");
    }

    private static PutawaySerial ReadSerial(PutawaySerialRow x) => new(x.SerialItemID, x.SerialNumber, x.ProductID, x.StockState,
        x.ItemCondition, x.IsQuarantined, x.CurrentWarehouseID ?? 0, x.CurrentLocationID, null, Convert.ToBase64String(x.RowVersion),
        new(x.ProfileProductId ?? 0, x.ProductCode ?? "", x.ProductName ?? "Thiếu hồ sơ sản phẩm", x.CategoryID ?? 0,
            x.CategoryName ?? "", x.BrandID ?? 0, x.BrandName ?? "", x.ProductStatus ?? "MISSING", x.LengthCm, x.WidthCm, x.HeightCm, x.GrossWeightKg),
        x.WarehouseName ?? "Kho không tồn tại", x.OriginalInboundID, x.ReceiptCode, DateTime.SpecifyKind(x.RegisteredAt, DateTimeKind.Utc),
        InboundStatus: x.InboundStatus, OriginType: x.OriginType);

    private static string Path(PutawayWarehouseSnapshot snapshot, int location)
    {
        var parts = new List<string>(); var seen = new HashSet<int>(); int? current = location;
        while (current is { } id && seen.Add(id))
        {
            var node = snapshot.Locations.SingleOrDefault(n => n.LocationId == id);
            if (node is null) break;
            parts.Add(node.LocationName); current = node.ParentLocationId;
        }
        parts.Add(snapshot.WarehouseName); parts.Reverse(); return string.Join(" → ", parts);
    }
    private static string Escape(string value) => value.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[");
    private static SqlParameter P(string name, SqlDbType type, object? value, int size = 0) =>
        new(name, type, size) { Value = value ?? DBNull.Value };
}
