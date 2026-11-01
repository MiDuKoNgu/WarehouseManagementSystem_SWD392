using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Inventory.BLL.Putaway;
using Inventory.Models.Putaway;
using Microsoft.Data.SqlClient;

namespace Inventory.DAL.Putaway;

/// <summary>Inventory owns writes. Domain configuration is read and locked on the same SQL instance; no DTC or cross-domain writes.</summary>
public sealed class SqlPutawayRepository : IPutawayRepository
{
    private readonly string connectionString;
    private readonly string catalog;
    private readonly string warehouse;
    private readonly IPutawayOperationGuard guard;
    public SqlPutawayRepository(string connectionString, IPutawayOperationGuard guard,
        string catalogDatabase = "CatalogDB", string warehouseDatabase = "WarehouseDB")
    {
        this.connectionString = connectionString; this.guard = guard;
        catalog = Database(catalogDatabase); warehouse = Database(warehouseDatabase);
    }
    private static string Database(string value) => Regex.IsMatch(value, @"\A[A-Za-z0-9_]{1,128}\z")
        ? "[" + value + "]" : throw new ArgumentException("Invalid Putaway database configuration.");
    private string ProfileColumns => "p.ProductID,p.ProductCode,p.ProductName,p.CategoryID,c.CategoryName,p.BrandID,b.BrandName,CASE WHEN c.CategoryID IS NULL OR b.BrandID IS NULL THEN 'MISSING' ELSE p.Status END,p.LengthCm,p.WidthCm,p.HeightCm,p.GrossWeightKg";
    private string ProductJoin(string hint) => $"LEFT JOIN {catalog}.dbo.Product p {hint} ON p.ProductID=s.ProductID LEFT JOIN {catalog}.dbo.Category c {hint} ON c.CategoryID=p.CategoryID LEFT JOIN {catalog}.dbo.Brand b {hint} ON b.BrandID=p.BrandID";
    private string SerialSql(bool locked) => $"""
        SELECT s.SerialItemID,s.SerialNumber,s.ProductID,s.StockState,s.ItemCondition,s.IsQuarantined,
            s.CurrentWarehouseID,s.CurrentLocationID,s.RowVersion,s.OriginalInboundID,i.ReceiptCode,s.RegisteredAt,
            CASE WHEN i.InboundID IS NULL THEN NULL WHEN UPPER(i.Status) IN ('DONE','COMPLETED') AND i.CompletedAt IS NOT NULL THEN 'DONE' ELSE 'NOT_DONE' END,
            s.OriginType,w.WarehouseName,{ProfileColumns}
        FROM dbo.SerialItem s {(locked ? "WITH (UPDLOCK,HOLDLOCK)" : "")}
        LEFT JOIN dbo.Inbound i {(locked ? "WITH (HOLDLOCK)" : "")} ON i.InboundID=s.OriginalInboundID
        LEFT JOIN {warehouse}.dbo.Warehouse w {(locked ? "WITH (HOLDLOCK)" : "")} ON w.WarehouseID=s.CurrentWarehouseID
        {ProductJoin(locked ? "WITH (HOLDLOCK)" : "")}
        """;

    public async Task<PutawayPendingPage> PendingAsync(PutawayActor actor, string? search, int page, int pageSize, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        var filter = """
            WHERE s.CurrentWarehouseID=@warehouse AND s.StockState='IN_WAREHOUSE' AND s.CurrentLocationID IS NULL
                AND ((s.OriginalInboundID IS NULL AND s.OriginType<>'PURCHASE') OR (UPPER(i.Status) IN ('DONE','COMPLETED') AND i.CompletedAt IS NOT NULL))
                AND (@search IS NULL OR s.SerialNumber LIKE @search ESCAPE '~' OR p.ProductCode LIKE @search ESCAPE '~' OR p.ProductName LIKE @search ESCAPE '~' OR i.ReceiptCode LIKE @search ESCAPE '~')
            """;
        await using var command = new SqlCommand(SerialSql(false) + "\n" + filter + " ORDER BY s.RegisteredAt,s.SerialItemID OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY; "
            + $"SELECT COUNT(*) FROM dbo.SerialItem s LEFT JOIN dbo.Inbound i ON i.InboundID=s.OriginalInboundID {ProductJoin("")} " + filter, connection);
        P(command,"@warehouse",SqlDbType.Int,actor.WarehouseId);
        P(command,"@skip",SqlDbType.Int,checked((page-1)*pageSize)); P(command,"@take",SqlDbType.Int,pageSize);
        P(command,"@search",SqlDbType.NVarChar,string.IsNullOrEmpty(search)?null:"%"+Escape(search)+"%",402);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var items = new List<PutawaySerial>();
        while(await reader.ReadAsync(ct)) items.Add(ReadSerial(reader));
        await reader.NextResultAsync(ct); await reader.ReadAsync(ct);
        return new(items.ToArray(),reader.GetInt32(0),page,pageSize);
    }
    public async Task<PutawaySerial> FindAsync(PutawayActor actor,long? id,string? serial,CancellationToken ct)
    {
        await using var connection=await Open(ct);
        var item=await ReadItem(connection,null,actor,id,serial,ct);
        if(item.CurrentLocationId is { } location)
            item=item with {CurrentLocationPath=Path(await Snapshot(connection,null,actor.WarehouseId,ct),location)};
        return item;
    }
    public async Task<PutawaySuggestions> SuggestionsAsync(PutawayActor actor,long id,CancellationToken ct)
    {
        await using var connection=await Open(ct);
        await AssertSupportedControls(connection,null,ct);
        var item=await ReadItem(connection,null,actor,id,null,ct);
        var snapshot=await Snapshot(connection,null,actor.WarehouseId,ct);
        item=item with {CurrentLocationPath=item.CurrentLocationId is { } location?Path(snapshot,location):null};
        PutawayRules.ValidateSerial(item,actor.WarehouseId); await guard.ValidateAsync(item,snapshot,ct);
        return PutawayRules.Suggest(item,snapshot,await Occupants(connection,null,actor.WarehouseId,null,ct));
    }
    public async Task<PutawayResult> ConfirmAsync(PutawayActor actor,ConfirmPutaway request,CancellationToken ct)
    {
        var version=PutawayRules.Version(request.RowVersion);
        await using var connection=await Open(ct);
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
        try
        {
            await using(var timeout=new SqlCommand("SET LOCK_TIMEOUT 10000;",connection,transaction)) await timeout.ExecuteNonQueryAsync(ct);
            // Target first: a waiter must not hold a serial row lock needed by the winner's occupancy scan.
            await Lock(connection,transaction,$"EWMS:Inventory:Location:{actor.WarehouseId}:{request.TargetLocationId}",ct);
            await AssertSupportedControls(connection,transaction,ct);
            var item=await ReadItem(connection,transaction,actor,request.SerialItemId,null,ct);
            var snapshot=await Snapshot(connection,transaction,actor.WarehouseId,ct);
            item=item with {CurrentLocationPath=item.CurrentLocationId is { } current?Path(snapshot,current):null};
            PutawayRules.ValidateSerial(item,actor.WarehouseId);
            if(!Convert.FromBase64String(item.RowVersion).AsSpan().SequenceEqual(version))
                throw new PutawayException(409,"STALE_SERIAL","Serial đã thay đổi. Hãy tải lại thông tin trước khi xác nhận.");
            await guard.ValidateAsync(item,snapshot,ct);
            var target=snapshot.Locations.SingleOrDefault(node=>node.LocationId==request.TargetLocationId)
                ?? throw new PutawayException(404,"LOCATION_NOT_FOUND","Không tìm thấy vị trí trong kho được phân công.");
            // HOLDLOCK protects the occupancy predicate from other writers through commit as well.
            var occupants=await Occupants(connection,transaction,actor.WarehouseId,request.TargetLocationId,ct);
            var candidate=PutawayRules.Evaluate(item,snapshot,target,occupants);
            if(!candidate.IsValid) throw new PutawayException(409,"LOCATION_UNAVAILABLE",candidate.Reason??"Vị trí không còn hợp lệ.");
            var occurred=DateTime.UtcNow;
            string newVersion;
            await using(var update=new SqlCommand("""
                UPDATE dbo.SerialItem SET CurrentLocationID=@location OUTPUT INSERTED.RowVersion
                WHERE SerialItemID=@id AND CurrentWarehouseID=@warehouse AND StockState='IN_WAREHOUSE'
                    AND CurrentLocationID IS NULL AND RowVersion=@version
                """,connection,transaction))
            {
                P(update,"@location",SqlDbType.Int,request.TargetLocationId); P(update,"@id",SqlDbType.BigInt,request.SerialItemId);
                P(update,"@warehouse",SqlDbType.Int,actor.WarehouseId); P(update,"@version",SqlDbType.Binary,version,8);
                if(await update.ExecuteScalarAsync(ct) is not byte[] bytes) throw new PutawayException(409,"STALE_SERIAL","Serial đã thay đổi hoặc đã được Putaway.");
                newVersion=Convert.ToBase64String(bytes);
            }
            await using(var movement=new SqlCommand("""
                INSERT dbo.SerialMovement(SerialItemID,PerformedByUserID,EventType,OccurredAt,FromState,ToState,
                    FromCondition,ToCondition,FromWarehouseID,ToWarehouseID,FromLocationID,ToLocationID,FromQuarantined,ToQuarantined,Note)
                VALUES(@id,@actor,'PUTAWAY',@at,'IN_WAREHOUSE','IN_WAREHOUSE',@condition,@condition,
                    @warehouse,@warehouse,NULL,@location,@quarantine,@quarantine,@note)
                """,connection,transaction))
            {
                P(movement,"@id",SqlDbType.BigInt,item.SerialItemId); P(movement,"@actor",SqlDbType.Int,actor.UserId);
                P(movement,"@at",SqlDbType.DateTime2,occurred); P(movement,"@condition",SqlDbType.VarChar,item.ItemCondition,8);
                P(movement,"@warehouse",SqlDbType.Int,actor.WarehouseId); P(movement,"@location",SqlDbType.Int,target.LocationId);
                P(movement,"@quarantine",SqlDbType.Bit,item.IsQuarantined);
                P(movement,"@note",SqlDbType.NVarChar,$"Putaway: {candidate.LocationPath}; {actor.Name}",1000);
                await movement.ExecuteNonQueryAsync(ct);
            }
            await using(var audit=new SqlCommand("""
                INSERT dbo.AuditLog(UserID,Action,EntityName,EntityID,OldValues,NewValues,Status,CreatedAt)
                VALUES(@actor,'PUTAWAY_COMPLETED','Putaway',@entity,@old,@new,'SUCCESS',@at)
                """,connection,transaction))
            {
                P(audit,"@actor",SqlDbType.Int,actor.UserId); P(audit,"@entity",SqlDbType.NVarChar,item.SerialItemId.ToString(CultureInfo.InvariantCulture),100); P(audit,"@at",SqlDbType.DateTime2,occurred);
                P(audit,"@old",SqlDbType.NVarChar,JsonSerializer.Serialize(new {item.SerialItemId,currentLocationId=(int?)null,item.StockState,item.ItemCondition,item.IsQuarantined,item.RowVersion}),-1);
                P(audit,"@new",SqlDbType.NVarChar,JsonSerializer.Serialize(new {item.SerialItemId,item.SerialNumber,currentLocationId=target.LocationId,
                    locationPath=candidate.LocationPath,warehouseId=actor.WarehouseId,item.StockState,item.ItemCondition,item.IsQuarantined,
                    inboundId=item.OriginalInboundId,performedByUserId=actor.UserId,performedByName=actor.Name,rowVersion=newVersion}),-1);
                await audit.ExecuteNonQueryAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return new(item.SerialItemId,item.SerialNumber,target.LocationId,candidate.LocationPath,occurred,actor.UserId,actor.Name,newVersion);
        }
        catch(SqlException error) when(error.Number is 1205 or 1222)
        { throw new PutawayException(409,"CONCURRENT_PUTAWAY","Vị trí hoặc Serial đang được xử lý. Hãy tải lại gợi ý và thử lại."); }
        // Dispose rolls back uncommitted writes, including cancellation and movement/audit errors.
    }
    private async Task<PutawaySerial> ReadItem(SqlConnection connection,SqlTransaction? transaction,PutawayActor actor,long? id,string? serial,CancellationToken ct)
    {
        await using var command=new SqlCommand(SerialSql(transaction is not null)+" WHERE s.CurrentWarehouseID=@warehouse AND "+(id is not null?"s.SerialItemID=@id":"s.SerialNumber=@serial"),connection,transaction);
        P(command,"@warehouse",SqlDbType.Int,actor.WarehouseId); P(command,"@id",SqlDbType.BigInt,id); P(command,"@serial",SqlDbType.NVarChar,serial,100);
        await using var reader=await command.ExecuteReaderAsync(ct);
        if(!await reader.ReadAsync(ct)) throw new PutawayException(404,"SERIAL_NOT_FOUND","Không tìm thấy Serial trong kho được phân công (hoặc hàng đã rời kho).");
        return ReadSerial(reader);
    }
    private async Task<PutawayWarehouseSnapshot> Snapshot(SqlConnection connection,SqlTransaction? transaction,int warehouseId,CancellationToken ct)
    {
        var hint=transaction is null?"":"WITH (HOLDLOCK)";
        await using var command=new SqlCommand($"""
            SELECT WarehouseName,Status,WarehouseType,RentalEndDate FROM {warehouse}.dbo.Warehouse {hint} WHERE WarehouseID=@warehouse;
            SELECT LocationID,WarehouseID,ParentLocationID,LocationName,LocationType,StorageType,CapacityMode,ConditionPolicy,ZoneType,Status,LengthCm,WidthCm,HeightCm,MaxWeightKg
            FROM {warehouse}.dbo.StorageLocation {hint} WHERE WarehouseID=@warehouse ORDER BY LocationID;
            SELECT r.ZoneLocationID,r.CategoryID,r.BrandID FROM {warehouse}.dbo.ZoneStorageRule r {hint}
            JOIN {warehouse}.dbo.StorageLocation z {hint} ON z.LocationID=r.ZoneLocationID WHERE z.WarehouseID=@warehouse;
            """,connection,transaction);
        P(command,"@warehouse",SqlDbType.Int,warehouseId);
        await using var reader=await command.ExecuteReaderAsync(ct);
        if(!await reader.ReadAsync(ct)) throw new PutawayException(404,"WAREHOUSE_NOT_FOUND","Kho được phân công không tồn tại.");
        var name=reader.GetString(0);var status=reader.GetString(1);var type=reader.GetString(2);var end=reader.IsDBNull(3)?(DateTime?)null:reader.GetDateTime(3);
        var locations=new List<PutawayLocationNode>();await reader.NextResultAsync(ct);
        while(await reader.ReadAsync(ct)) locations.Add(new(reader.GetInt32(0),reader.GetInt32(1),NullableInt(reader,2),reader.GetString(3),reader.GetString(4),
            Text(reader,5),Text(reader,6),Text(reader,7),Text(reader,8),reader.GetString(9),Number(reader,10),Number(reader,11),Number(reader,12),Number(reader,13)));
        var rules=new List<PutawayZoneRule>();await reader.NextResultAsync(ct);
        while(await reader.ReadAsync(ct)) rules.Add(new(reader.GetInt32(0),reader.GetInt32(1),NullableInt(reader,2)));
        return new(warehouseId,name,status,locations.ToArray(),rules.ToArray(),type,end);
    }
    private async Task<PutawayOccupant[]> Occupants(SqlConnection connection,SqlTransaction? transaction,int warehouseId,int? location,CancellationToken ct)
    {
        var hint=transaction is null?"":"WITH (HOLDLOCK)";
        await using var command=new SqlCommand($"""
            SELECT s.SerialItemID,s.CurrentLocationID,s.ProductID,s.ItemCondition,{ProfileColumns}
            FROM dbo.SerialItem s {hint} {ProductJoin(hint)}
            WHERE s.CurrentWarehouseID=@warehouse AND s.StockState='IN_WAREHOUSE' AND s.CurrentLocationID IS NOT NULL
                AND (@location IS NULL OR s.CurrentLocationID=@location)
            """,connection,transaction);
        P(command,"@warehouse",SqlDbType.Int,warehouseId);P(command,"@location",SqlDbType.Int,location);
        await using var reader=await command.ExecuteReaderAsync(ct);var result=new List<PutawayOccupant>();
        while(await reader.ReadAsync(ct)) result.Add(new(reader.GetInt64(0),reader.GetInt32(1),reader.GetInt32(2),reader.GetString(3),ReadProduct(reader,4)));
        return result.ToArray();
    }
    private static async Task AssertSupportedControls(SqlConnection connection,SqlTransaction? transaction,CancellationToken ct)
    {
        // Current schema has no claim/stocktake module. Fail closed if added before its guard is integrated.
        await using var command=new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE name LIKE '%Stocktake%' OR name LIKE '%SerialClaim%' OR name LIKE '%PickingSession%' OR name LIKE '%PickingClaim%'",connection,transaction);
        if(Convert.ToInt32(await command.ExecuteScalarAsync(ct))>0)
            throw new PutawayException(503,"MOVEMENT_GUARD_REQUIRED","Schema đã có kiểm kê/claim. Cần tích hợp bộ kiểm soát di chuyển trước khi Putaway.");
    }
    private static async Task Lock(SqlConnection connection,SqlTransaction transaction,string resource,CancellationToken ct)
    {
        await using var command=new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; SELECT @result;",connection,transaction);
        P(command,"@resource",SqlDbType.NVarChar,resource,255);
        if(Convert.ToInt32(await command.ExecuteScalarAsync(ct))<0) throw new PutawayException(409,"LOCATION_BUSY","Vị trí đang được xử lý. Hãy thử lại.");
    }
    private async Task<SqlConnection> Open(CancellationToken ct)
    {var connection=new SqlConnection(connectionString);try{await connection.OpenAsync(ct);return connection;}catch{await connection.DisposeAsync();throw;}}
    private static PutawaySerial ReadSerial(SqlDataReader r)=>new(r.GetInt64(0),r.GetString(1),r.GetInt32(2),r.GetString(3),r.GetString(4),r.GetBoolean(5),
        NullableInt(r,6)??0,NullableInt(r,7),null,Convert.ToBase64String((byte[])r[8]),ReadProduct(r,15),Text(r,14)??"Kho không tồn tại",
        r.IsDBNull(9)?null:r.GetInt64(9),Text(r,10),DateTime.SpecifyKind(r.GetDateTime(11),DateTimeKind.Utc),InboundStatus:Text(r,12),OriginType:Text(r,13));
    private static ProductStorageProfile ReadProduct(SqlDataReader r,int at)=>new(NullableInt(r,at)??0,Text(r,at+1)??"",Text(r,at+2)??"Thiếu hồ sơ sản phẩm",
        NullableInt(r,at+3)??0,Text(r,at+4)??"",NullableInt(r,at+5)??0,Text(r,at+6)??"",Text(r,at+7)??"MISSING",Number(r,at+8),Number(r,at+9),Number(r,at+10),Number(r,at+11));
    private static string Path(PutawayWarehouseSnapshot snapshot,int location)
    {
        var parts=new List<string>();var seen=new HashSet<int>();int? current=location;
        while(current is { } id && seen.Add(id)){var node=snapshot.Locations.SingleOrDefault(n=>n.LocationId==id);if(node is null)break;parts.Add(node.LocationName);current=node.ParentLocationId;}
        parts.Add(snapshot.WarehouseName);parts.Reverse();return string.Join(" → ",parts);
    }
    private static string Escape(string v)=>v.Replace("~","~~").Replace("%","~%").Replace("_","~_").Replace("[","~[");
    private static string? Text(SqlDataReader r,int at)=>r.IsDBNull(at)?null:r.GetString(at);
    private static int? NullableInt(SqlDataReader r,int at)=>r.IsDBNull(at)?null:r.GetInt32(at);
    private static decimal? Number(SqlDataReader r,int at)=>r.IsDBNull(at)?null:r.GetDecimal(at);
    private static void P(SqlCommand cmd,string name,SqlDbType type,object? value,int size=0)
    {var p=size==0?cmd.Parameters.Add(name,type):cmd.Parameters.Add(name,type,size);p.Value=value??DBNull.Value;}
}
