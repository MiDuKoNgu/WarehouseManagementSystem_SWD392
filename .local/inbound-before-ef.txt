using System.Data;
using System.Text.Json;
using Inventory.BLL.Inbound;
using Inventory.Models.Inbound;
using Microsoft.Data.SqlClient;

namespace Inventory.DAL.Inbound;

public sealed class SqlInboundRepository(string connectionString, ReceiptPolicy policy)
{
    private const string HeaderSql = """
        SELECT r.InboundID,r.PurchaseOrderID,r.ReceiptCode,r.Status,r.ReceiptDate,r.CreatedAt,
            r.CreatedByUserID,r.CreatedByName,r.CompletedAt,r.PerformedByUserID,r.PerformedByName,
            r.Note,r.RowVersion,r.WarehouseID,
            (SELECT COUNT(*) FROM dbo.InboundItem i WHERE i.InboundID=r.InboundID AND i.IsActive=1)
        FROM dbo.Inbound r
        """;
    public async Task<ReceiptHeader[]> ListAsync(ReceiptActor actor, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await using var command = new SqlCommand(HeaderSql + " WHERE r.WarehouseID=@warehouse ORDER BY r.CreatedAt DESC,r.InboundID DESC", connection);
        P(command,"@warehouse",SqlDbType.Int,actor.WarehouseId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<ReceiptHeader>();
        while (await reader.ReadAsync(ct)) result.Add(ReadHeader(reader));
        return result.ToArray();
    }
    public async Task<ReceiptHeader?> FindAsync(long id, ReceiptActor actor, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        return await Header(connection,null,id,actor,false,ct);
    }
    public async Task<ReceiptHeader?> FindByTokenAsync(Guid token, ReceiptActor actor, CancellationToken ct)
    {
        await using var connection=await Open(ct);
        await using var command=new SqlCommand(HeaderSql+" WHERE r.CreationToken=@token AND r.WarehouseID=@warehouse",connection);
        P(command,"@token",SqlDbType.UniqueIdentifier,token);P(command,"@warehouse",SqlDbType.Int,actor.WarehouseId);
        await using var reader=await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)?ReadHeader(reader):null;
    }
    public async Task<ReceiptItem[]> ItemsAsync(long id, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        return await Items(connection,null,id,ct);
    }
    public async Task<ReceiptProgress[]> ProgressAsync(ReceiptOrder order, long? currentId, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        return await Progress(connection,null,order,currentId,ct);
    }
    public async Task<long> CreateAsync(ReceiptOrder order, string? note, ReceiptActor actor, CancellationToken ct, Guid? creationToken=null)
    {
        CheckOrder(order,actor);
        if (note?.Length>1000) throw new ReceiptException(400,"Ghi chú tối đa 1000 ký tự.");
        return await Write(async (connection,transaction) =>
        {
            if(creationToken is { } token)
            {
                await using var retry=new SqlCommand("SELECT InboundID,PurchaseOrderID,WarehouseID,Status FROM dbo.Inbound WHERE CreationToken=@token",connection,transaction);
                P(retry,"@token",SqlDbType.UniqueIdentifier,token);
                await using var reader=await retry.ExecuteReaderAsync(ct);
                if(await reader.ReadAsync(ct))
                {
                    if(reader.GetInt64(1)!=order.PurchaseOrderId||reader.GetInt32(2)!=actor.WarehouseId)
                        throw new ReceiptException(409,"Mã yêu cầu đã thuộc phiếu nhập khác.");
                    if(reader.GetString(3).Equals("CANCELLED",StringComparison.OrdinalIgnoreCase))
                        throw new ReceiptException(409,"Yêu cầu tạo phiếu đã bị hủy. Vui lòng bắt đầu yêu cầu mới.");
                    return reader.GetInt64(0);
                }
            }
            if (!policy.AllowMultipleDrafts)
            {
                await using var existing = new SqlCommand("SELECT COUNT(*) FROM dbo.Inbound WHERE PurchaseOrderID=@po AND UPPER(Status) NOT IN ('DONE','COMPLETED','CANCELLED')",connection,transaction);
                P(existing,"@po",SqlDbType.BigInt,order.PurchaseOrderId);
                if (Convert.ToInt32(await existing.ExecuteScalarAsync(ct))>0) throw new ReceiptException(409,"PO đã có phiếu nhập đang mở. Vui lòng tiếp tục phiếu đó.");
            }
            var progress = await Progress(connection,transaction,order,null,ct);
            if (!progress.Any(line=>line.RemainingQuantity>0)) throw new ReceiptException(409,"PO không còn số lượng được nhận.");
            var code=$"IN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..36];
            await using var command = new SqlCommand("""
                INSERT dbo.Inbound(PurchaseOrderID,CreatedByUserID,ReceiptCode,ReceiptDate,Status,CreatedAt,Note,WarehouseID,CreatedByName,CreationToken)
                OUTPUT INSERTED.InboundID VALUES(@po,@actor,@code,@date,'DRAFT',SYSUTCDATETIME(),@note,@warehouse,@name,@token)
                """,connection,transaction);
            P(command,"@po",SqlDbType.BigInt,order.PurchaseOrderId); Actor(command,actor);
            P(command,"@code",SqlDbType.VarChar,code,40); P(command,"@date",SqlDbType.Date,InboundRules.Today().ToDateTime(TimeOnly.MinValue));
            P(command,"@note",SqlDbType.NVarChar,note?.Trim(),1000);
            P(command,"@token",SqlDbType.UniqueIdentifier,creationToken);
            var id=Convert.ToInt64(await command.ExecuteScalarAsync(ct));
            await Audit(connection,transaction,id,"INBOUND_CREATED",actor,null,new { inboundId=id,order.PurchaseOrderId,receiptCode=code,status="DRAFT",createdByName=actor.Name },ct);
            return id;
        },ct);
    }
    public async Task<bool> AbortCreationAsync(ReceiptOrder order,ReceiptActor actor,Guid token,string reason,CancellationToken ct)
    {
        if(actor.WarehouseId<=0||order.WarehouseId!=actor.WarehouseId)
            throw new ReceiptException(403,"PO không thuộc kho được phân công.");
        return await Write(async(connection,transaction)=>
        {
            // The same lock as Create fences late retries before releasing the Procurement reservation.
            await using(var existing=new SqlCommand("SELECT PurchaseOrderID,WarehouseID,Status FROM dbo.Inbound WHERE CreationToken=@token",connection,transaction))
            {
                P(existing,"@token",SqlDbType.UniqueIdentifier,token);
                await using var reader=await existing.ExecuteReaderAsync(ct);
                if(await reader.ReadAsync(ct))
                {
                    if(reader.GetInt64(0)!=order.PurchaseOrderId||reader.GetInt32(1)!=actor.WarehouseId)
                        throw new ReceiptException(409,"Mã yêu cầu đã thuộc phiếu nhập khác.");
                    return reader.GetString(2).ToUpperInvariant() is "DONE" or "COMPLETED" or "CANCELLED";
                }
            }
            var code=$"IN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..36];
            var note="Tạo phiếu thất bại: "+reason;
            if(note.Length>1000) note=note[..1000];
            await using var command=new SqlCommand("""
                INSERT dbo.Inbound(PurchaseOrderID,CreatedByUserID,ReceiptCode,ReceiptDate,Status,CreatedAt,Note,
                    WarehouseID,CreatedByName,PerformedByUserID,PerformedByName,CreationToken)
                OUTPUT INSERTED.InboundID
                VALUES(@po,@actor,@code,@date,'CANCELLED',SYSUTCDATETIME(),@note,@warehouse,@name,@actor,@name,@token)
                """,connection,transaction);
            P(command,"@po",SqlDbType.BigInt,order.PurchaseOrderId);Actor(command,actor);
            P(command,"@code",SqlDbType.VarChar,code,40);P(command,"@date",SqlDbType.Date,InboundRules.Today().ToDateTime(TimeOnly.MinValue));
            P(command,"@note",SqlDbType.NVarChar,note,1000);P(command,"@token",SqlDbType.UniqueIdentifier,token);
            var id=Convert.ToInt64(await command.ExecuteScalarAsync(ct));
            await Audit(connection,transaction,id,"INBOUND_CANCELLED",actor,null,
                new { inboundId=id,order.PurchaseOrderId,receiptCode=code,status="CANCELLED",reason=note,creationToken=token,creationFailed=true },ct);
            return true;
        },ct);
    }
    public async Task SaveItemAsync(long id, long? itemId, SaveReceiptItem request, ReceiptOrder order, ReceiptActor actor, CancellationToken ct)
    {
        request=InboundRules.Validate(request,policy,InboundRules.Today()); CheckOrder(order,actor);
        await Write(async (connection,transaction)=>
        {
            var header=await Draft(connection,transaction,id,request.RowVersion,actor,ct);
            if (header.PurchaseOrderId!=order.PurchaseOrderId) throw new ReceiptException(409,"PO không khớp phiếu nhập.");
            if (!order.Lines.Any(line=>line.PoLineId==request.PoLineId)) throw new ReceiptException(400,"Dòng sản phẩm không thuộc PO.");
            var items=await Items(connection,transaction,id,ct);
            var before=itemId is null ? null : items.SingleOrDefault(item=>item.InboundItemId==itemId);
            if (itemId is not null && before is null) throw new ReceiptException(404,"Không tìm thấy Serial đang hoạt động.");
            await CheckSerial(connection,transaction,request.Serial,itemId,ct);
            var line=(await Progress(connection,transaction,order,id,ct)).Single(line=>line.PoLineId==request.PoLineId);
            var available=line.RemainingQuantity + (before?.PoLineId==request.PoLineId ? 1 : 0);
            if (available<=0) throw new ReceiptException(409,"Dòng PO đã đủ số lượng hoặc được giữ trong phiếu khác.");
            var sql=itemId is null ? """
                INSERT dbo.InboundItem(InboundID,POLineID,ScannedSerial,ManufacturingMonth,ManufacturingYear,ReceivedCondition,
                    ConditionNote,ScannedAt,IsActive,InputMethod,ScannedByUserID,ScannedByName)
                OUTPUT INSERTED.InboundItemID VALUES(@id,@line,@serial,@month,@year,@condition,@note,SYSUTCDATETIME(),1,@method,@actor,@name)
                """ : """
                UPDATE dbo.InboundItem SET POLineID=@line,ScannedSerial=@serial,ManufacturingMonth=@month,ManufacturingYear=@year,
                    ReceivedCondition=@condition,ConditionNote=@note,InputMethod=@method
                OUTPUT INSERTED.InboundItemID WHERE InboundItemID=@item AND InboundID=@id AND IsActive=1
                """;
            await using var command=new SqlCommand(sql,connection,transaction);
            P(command,"@id",SqlDbType.BigInt,id); P(command,"@item",SqlDbType.BigInt,itemId); Actor(command,actor);
            ItemParameters(command,request);
            var saved=Convert.ToInt64(await command.ExecuteScalarAsync(ct));
            await Touch(connection,transaction,id,ct);
            await Audit(connection,transaction,id,itemId is null ? "INBOUND_SERIAL_ADDED" : "INBOUND_SERIAL_UPDATED",actor,before,
                new { inboundId=id,inboundItemId=saved,request.PoLineId,serial=request.Serial,request.ManufacturingMonth,request.ManufacturingYear,
                    request.ReceivedCondition,request.ConditionNote,request.InputMethod },ct);
            return true;
        },ct);
    }
    public async Task RemoveItemAsync(long id,long itemId,string version,ReceiptActor actor,CancellationToken ct)
    {
        await Write(async(connection,transaction)=>
        {
            await Draft(connection,transaction,id,version,actor,ct);
            var before=(await Items(connection,transaction,id,ct)).SingleOrDefault(item=>item.InboundItemId==itemId)
                ?? throw new ReceiptException(404,"Không tìm thấy Serial đang hoạt động.");
            await using var command=new SqlCommand("UPDATE dbo.InboundItem SET IsActive=0 WHERE InboundID=@id AND InboundItemID=@item AND IsActive=1",connection,transaction);
            P(command,"@id",SqlDbType.BigInt,id);P(command,"@item",SqlDbType.BigInt,itemId);await command.ExecuteNonQueryAsync(ct);
            await Touch(connection,transaction,id,ct);
            await Audit(connection,transaction,id,"INBOUND_SERIAL_REMOVED",actor,before,new { inboundId=id,inboundItemId=itemId,serial=before.ScannedSerial,isActive=false },ct);
            return true;
        },ct);
    }
    public async Task CompleteAsync(long id,string version,ReceiptOrder order,ReceiptActor actor,CancellationToken ct)
    {
        CheckOrder(order,actor);
        await Write(async(connection,transaction)=>
        {
            var header=await Draft(connection,transaction,id,version,actor,ct);
            if(header.PurchaseOrderId!=order.PurchaseOrderId) throw new ReceiptException(409,"PO không khớp phiếu nhập.");
            var items=await Items(connection,transaction,id,ct);
            if(items.Length==0) throw new ReceiptException(400,"Cần ít nhất một Serial để hoàn tất phiếu.");
            foreach(var item in items)
            {
                InboundRules.Validate(new(item.PoLineId,item.ScannedSerial,item.ManufacturingMonth??0,item.ManufacturingYear??0,
                    item.ReceivedCondition??"",item.ConditionNote,item.InputMethod,version),policy,InboundRules.Today());
                if(item.SerialItemId is not null || !order.Lines.Any(line=>line.PoLineId==item.PoLineId))
                    throw new ReceiptException(409,"Serial hoặc dòng PO không hợp lệ để hoàn tất.");
                await CheckSerial(connection,transaction,item.ScannedSerial,item.InboundItemId,ct);
            }
            await Progress(connection,transaction,order,id,ct); // Fail closed on over-receipt or inconsistent historical rows.
            foreach(var group in items.GroupBy(item=>new { ProductId=order.Lines.Single(line=>line.PoLineId==item.PoLineId).ProductId, Condition=item.ReceivedCondition! }))
            {
                await using var source=new SqlCommand("""
                    INSERT dbo.SupplySource(ProductID,WarehouseID,ItemCondition,SourceType,SupplyState,InboundID,
                        EligibleQuantity,RemainingQuantity,AvailableFrom,CreatedAt)
                    OUTPUT INSERTED.SupplySourceID VALUES(@product,@warehouse,@condition,'INBOUND','PHYSICAL',@id,@eligible,@quantity,@date,SYSUTCDATETIME())
                    """,connection,transaction);
                P(source,"@product",SqlDbType.Int,group.Key.ProductId);P(source,"@warehouse",SqlDbType.Int,actor.WarehouseId);
                P(source,"@condition",SqlDbType.VarChar,group.Key.Condition,8);P(source,"@id",SqlDbType.BigInt,id);
                P(source,"@eligible",SqlDbType.Int,group.Key.Condition=="DAMAGED"?0:group.Count());P(source,"@quantity",SqlDbType.Int,group.Count());
                P(source,"@date",SqlDbType.Date,InboundRules.Today().ToDateTime(TimeOnly.MinValue));
                var sourceId=Convert.ToInt64(await source.ExecuteScalarAsync(ct));
                foreach(var item in group)
                {
                    await using var serial=new SqlCommand("""
                        INSERT dbo.SerialItem(ProductID,SerialNumber,ManufacturingMonth,ManufacturingYear,OriginType,OriginalInboundID,
                            CurrentWarehouseID,CurrentLocationID,CurrentSupplySourceID,StockState,ItemCondition,IsQuarantined,QuarantineReason,RegisteredAt)
                        OUTPUT INSERTED.SerialItemID
                        VALUES(@product,@serial,@month,@year,'PURCHASE',@id,@warehouse,NULL,@source,'IN_WAREHOUSE',@condition,@quarantine,@reason,SYSUTCDATETIME())
                        """,connection,transaction);
                    P(serial,"@product",SqlDbType.Int,group.Key.ProductId);P(serial,"@serial",SqlDbType.NVarChar,item.ScannedSerial,100);
                    P(serial,"@month",SqlDbType.TinyInt,item.ManufacturingMonth);P(serial,"@year",SqlDbType.SmallInt,item.ManufacturingYear);
                    P(serial,"@id",SqlDbType.BigInt,id);P(serial,"@warehouse",SqlDbType.Int,actor.WarehouseId);P(serial,"@source",SqlDbType.BigInt,sourceId);
                    P(serial,"@condition",SqlDbType.VarChar,item.ReceivedCondition,8);P(serial,"@quarantine",SqlDbType.Bit,item.ReceivedCondition=="DAMAGED");
                    P(serial,"@reason",SqlDbType.NVarChar,item.ReceivedCondition=="DAMAGED"?item.ConditionNote:null,1000);
                    var serialId=Convert.ToInt64(await serial.ExecuteScalarAsync(ct));
                    await using var link=new SqlCommand("UPDATE dbo.InboundItem SET SerialItemID=@serial WHERE InboundItemID=@item",connection,transaction);
                    P(link,"@serial",SqlDbType.BigInt,serialId);P(link,"@item",SqlDbType.BigInt,item.InboundItemId);await link.ExecuteNonQueryAsync(ct);
                    await using var movement=new SqlCommand("""
                        INSERT dbo.SerialMovement(SerialItemID,PerformedByUserID,EventType,OccurredAt,ToState,ToCondition,ToWarehouseID,ToLocationID,ToQuarantined,InboundID,Note)
                        VALUES(@serial,@actor,'PURCHASE_RECEIPT',SYSUTCDATETIME(),'IN_WAREHOUSE',@condition,@warehouse,NULL,@quarantine,@id,@note)
                        """,connection,transaction);
                    P(movement,"@serial",SqlDbType.BigInt,serialId);P(movement,"@actor",SqlDbType.Int,actor.UserId);
                    P(movement,"@condition",SqlDbType.VarChar,item.ReceivedCondition,8);P(movement,"@warehouse",SqlDbType.Int,actor.WarehouseId);
                    P(movement,"@quarantine",SqlDbType.Bit,item.ReceivedCondition=="DAMAGED");P(movement,"@id",SqlDbType.BigInt,id);
                    P(movement,"@note",SqlDbType.NVarChar,"Chờ Putaway",1000);await movement.ExecuteNonQueryAsync(ct);
                }
            }
            await using var finish=new SqlCommand("UPDATE dbo.Inbound SET Status='DONE',CompletedAt=SYSUTCDATETIME(),PerformedByUserID=@actor,PerformedByName=@name WHERE InboundID=@id",connection,transaction);
            P(finish,"@id",SqlDbType.BigInt,id);Actor(finish,actor);await finish.ExecuteNonQueryAsync(ct);
            await Audit(connection,transaction,id,"INBOUND_COMPLETED",actor,header,new { inboundId=id,status="DONE",totalSerials=items.Length,pendingPutaway=true },ct);
            return true;
        },ct);
    }
    public async Task CancelAsync(long id,string version,string reason,ReceiptActor actor,CancellationToken ct)
    {
        if(!policy.AllowCancellation) throw new ReceiptException(403,"Chính sách hiện tại không cho hủy toàn phiếu.");
        if(string.IsNullOrWhiteSpace(reason)||reason.Trim().Length>1000) throw new ReceiptException(400,"Lý do hủy cần từ 1 đến 1000 ký tự.");
        await Write(async(connection,transaction)=>
        {
            var before=await Draft(connection,transaction,id,version,actor,ct);
            await using var command=new SqlCommand("UPDATE dbo.InboundItem SET IsActive=0 WHERE InboundID=@id AND IsActive=1; UPDATE dbo.Inbound SET Status='CANCELLED',PerformedByUserID=@actor,PerformedByName=@name WHERE InboundID=@id",connection,transaction);
            P(command,"@id",SqlDbType.BigInt,id);Actor(command,actor);await command.ExecuteNonQueryAsync(ct);
            await Audit(connection,transaction,id,"INBOUND_CANCELLED",actor,before,new { inboundId=id,status="CANCELLED",reason=reason.Trim() },ct);
            return true;
        },ct);
    }
    private async Task<T> Write<T>(Func<SqlConnection,SqlTransaction,Task<T>> action,CancellationToken ct)
    {
        await using var connection=await Open(ct);
        using var transaction=connection.BeginTransaction();
        await using var command=new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource='EWMS:InboundWrite',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; SELECT @result",connection,transaction);
        if(Convert.ToInt32(await command.ExecuteScalarAsync(ct))<0) throw new ReceiptException(409,"Kho đang xử lý yêu cầu khác. Vui lòng thử lại.");
        try
        {
            var result=await action(connection,transaction);
            await transaction.CommitAsync(ct);return result;
        }
        catch(SqlException exception) when(exception.Number is 2601 or 2627)
        { throw new ReceiptException(409,"Serial đã tồn tại hoặc vừa được nhận trong yêu cầu khác."); }
    }
    private async Task<SqlConnection> Open(CancellationToken ct)
    {
        var connection=new SqlConnection(connectionString);
        try {await connection.OpenAsync(ct);return connection;}catch{await connection.DisposeAsync();throw;}
    }
    private static void CheckOrder(ReceiptOrder order,ReceiptActor actor)
    {
        if(actor.WarehouseId<=0||order.WarehouseId!=actor.WarehouseId) throw new ReceiptException(403,"PO không thuộc kho được phân công.");
        if(!order.Status.Equals("CONFIRMED",StringComparison.OrdinalIgnoreCase)||order.Lines.Length==0)
            throw new ReceiptException(409,"Chỉ được nhận hàng từ PO đã CONFIRMED có dòng sản phẩm.");
        if(order.Lines.Any(line=>line.ProductId<=0||line.PoLineId<=0||line.OrderedQuantity<=0||line.ClosedQuantity<0||line.ClosedQuantity>line.OrderedQuantity)
            ||order.Lines.Select(line=>line.PoLineId).Distinct().Count()!=order.Lines.Length)
            throw new ReceiptException(409,"Dữ liệu dòng PO không nhất quán.");
    }
    private static async Task<ReceiptHeader> Draft(SqlConnection connection,SqlTransaction transaction,long id,string version,ReceiptActor actor,CancellationToken ct)
    {
        InboundRules.Version(version);
        var header=await Header(connection,transaction,id,actor,true,ct)??throw new ReceiptException(404,"Không tìm thấy phiếu nhập trong kho của bạn.");
        if(header.Status!="DRAFT") throw new ReceiptException(409,"Chỉ được thao tác phiếu DRAFT.");
        if(header.RowVersion!=version) throw new ReceiptException(409,"Phiếu đã thay đổi. Vui lòng tải lại trước khi thao tác.");
        return header;
    }
    private static async Task<ReceiptHeader?> Header(SqlConnection connection,SqlTransaction? transaction,long id,ReceiptActor actor,bool locked,CancellationToken ct)
    {
        var sql=locked?HeaderSql.Replace("FROM dbo.Inbound r","FROM dbo.Inbound r WITH (UPDLOCK,HOLDLOCK)"):HeaderSql;
        await using var command=new SqlCommand(sql+" WHERE r.InboundID=@id AND r.WarehouseID=@warehouse",connection,transaction);
        P(command,"@id",SqlDbType.BigInt,id);P(command,"@warehouse",SqlDbType.Int,actor.WarehouseId);
        await using var reader=await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)?ReadHeader(reader):null;
    }
    private static ReceiptHeader ReadHeader(SqlDataReader reader)=>new(reader.GetInt64(0),reader.GetInt64(1),reader.GetString(2),
        reader.GetString(3).Equals("COMPLETED",StringComparison.OrdinalIgnoreCase)?"DONE":reader.GetString(3).ToUpperInvariant(),
        DateOnly.FromDateTime(reader.GetDateTime(4)),Utc(reader.GetDateTime(5)),reader.GetInt32(6),Text(reader,7),
        reader.IsDBNull(8)?null:Utc(reader.GetDateTime(8)),reader.IsDBNull(9)?null:reader.GetInt32(9),Text(reader,10),Text(reader,11),
        Convert.ToBase64String((byte[])reader[12]),reader.GetInt32(13),reader.GetInt32(14));
    private static async Task<ReceiptItem[]> Items(SqlConnection connection,SqlTransaction? transaction,long id,CancellationToken ct)
    {
        await using var command=new SqlCommand("""
            SELECT InboundItemID,POLineID,ScannedSerial,ManufacturingMonth,ManufacturingYear,ReceivedCondition,ConditionNote,
                InputMethod,ScannedByUserID,ScannedByName,ScannedAt,SerialItemID
            FROM dbo.InboundItem WHERE InboundID=@id AND IsActive=1 ORDER BY ScannedAt DESC,InboundItemID DESC
            """,connection,transaction);
        P(command,"@id",SqlDbType.BigInt,id);
        await using var reader=await command.ExecuteReaderAsync(ct);var items=new List<ReceiptItem>();
        while(await reader.ReadAsync(ct)) items.Add(new(reader.GetInt64(0),reader.GetInt64(1),reader.GetString(2),reader.IsDBNull(3)?null:reader.GetByte(3),
            reader.IsDBNull(4)?null:reader.GetInt16(4),Text(reader,5),Text(reader,6),reader.GetString(7),reader.IsDBNull(8)?null:reader.GetInt32(8),
            Text(reader,9),Utc(reader.GetDateTime(10)),reader.IsDBNull(11)?null:reader.GetInt64(11)));
        return items.ToArray();
    }
    private static async Task<ReceiptProgress[]> Progress(SqlConnection connection,SqlTransaction? transaction,ReceiptOrder order,long? current,CancellationToken ct)
    {
        await using var command=new SqlCommand("""
            SELECT i.POLineID,
                COUNT(DISTINCT CASE WHEN UPPER(r.Status) IN ('DONE','COMPLETED') AND r.CompletedAt IS NOT NULL THEN s.SerialItemID END) Received,
                SUM(CASE WHEN UPPER(r.Status)='DRAFT' AND (@current IS NULL OR r.InboundID<>@current) THEN 1 ELSE 0 END) Reserved,
                SUM(CASE WHEN r.InboundID=@current THEN 1 ELSE 0 END) CurrentCount,
                SUM(CASE WHEN r.InboundID=@current AND UPPER(r.Status)='DRAFT' THEN 1 ELSE 0 END) CurrentReserved,
                SUM(CASE WHEN UPPER(r.Status) IN ('DONE','COMPLETED') AND (r.CompletedAt IS NULL OR s.SerialItemID IS NULL) THEN 1 ELSE 0 END) Invalid
            FROM dbo.Inbound r JOIN dbo.InboundItem i ON i.InboundID=r.InboundID AND i.IsActive=1
            LEFT JOIN dbo.SerialItem s ON s.SerialItemID=i.SerialItemID
            WHERE r.PurchaseOrderID=@po AND UPPER(r.Status)<>'CANCELLED' GROUP BY i.POLineID
            """,connection,transaction);
        P(command,"@po",SqlDbType.BigInt,order.PurchaseOrderId);P(command,"@current",SqlDbType.BigInt,current);
        await using var reader=await command.ExecuteReaderAsync(ct);
        var counts=new Dictionary<long,(int Received,int Reserved,int Current,int Draft)>();
        while(await reader.ReadAsync(ct))
        {
            var line=reader.GetInt64(0);
            if(reader.GetInt32(5)>0||!order.Lines.Any(item=>item.PoLineId==line)) throw new ReceiptException(409,"Dữ liệu nhận hàng lịch sử không khớp PO. Vui lòng kiểm tra.");
            counts[line]=(reader.GetInt32(1),reader.GetInt32(2),reader.GetInt32(3),reader.GetInt32(4));
        }
        return order.Lines.Select(line=>
        {
            var count=counts.GetValueOrDefault(line.PoLineId);
            var remaining=(long)line.OrderedQuantity-line.ClosedQuantity-count.Received-count.Reserved-count.Draft;
            if(remaining<0) throw new ReceiptException(409,"Tổng số lượng nhận và giữ chỗ đã vượt PO.");
            return new ReceiptProgress(line.PoLineId,line.ProductId,line.ProductCode,line.ProductName,line.OrderedQuantity,line.ClosedQuantity,
                count.Received,count.Reserved,count.Current,(int)remaining);
        }).ToArray();
    }
    private static async Task CheckSerial(SqlConnection connection,SqlTransaction transaction,string serial,long? excluded,CancellationToken ct)
    {
        await using var command=new SqlCommand("""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.SerialItem WHERE SerialNumber=@serial)
                OR EXISTS(SELECT 1 FROM dbo.InboundItem WHERE ScannedSerial=@serial AND IsActive=1 AND (@excluded IS NULL OR InboundItemID<>@excluded))
                THEN 1 ELSE 0 END
            """,connection,transaction);
        P(command,"@serial",SqlDbType.NVarChar,serial,100);P(command,"@excluded",SqlDbType.BigInt,excluded);
        if(Convert.ToInt32(await command.ExecuteScalarAsync(ct))!=0) throw new ReceiptException(409,"Serial đã tồn tại hoặc đang được giữ trong phiếu nhập khác.");
    }
    private static async Task Touch(SqlConnection connection,SqlTransaction transaction,long id,CancellationToken ct)
    {
        await using var command=new SqlCommand("UPDATE dbo.Inbound SET Note=Note WHERE InboundID=@id",connection,transaction);
        P(command,"@id",SqlDbType.BigInt,id);await command.ExecuteNonQueryAsync(ct);
    }
    private static async Task Audit(SqlConnection connection,SqlTransaction transaction,long id,string action,ReceiptActor actor,object? before,object after,CancellationToken ct)
    {
        await using var command=new SqlCommand("""
            INSERT dbo.AuditLog(UserID,Action,EntityName,EntityID,OldValues,NewValues,Status,CreatedAt)
            VALUES(@actor,@action,'Inbound',@id,@old,@new,'SUCCESS',SYSUTCDATETIME())
            """,connection,transaction);
        P(command,"@actor",SqlDbType.Int,actor.UserId);P(command,"@action",SqlDbType.VarChar,action,60);
        P(command,"@id",SqlDbType.NVarChar,id.ToString(System.Globalization.CultureInfo.InvariantCulture),100);
        P(command,"@old",SqlDbType.NVarChar,before is null?null:JsonSerializer.Serialize(before),-1);
        // Keep a stable root-level actor and inbound ID for the existing audit reader.
        var payload=JsonSerializer.SerializeToElement(after);
        var values=payload.EnumerateObject().ToDictionary(property=>property.Name,property=>(object?)property.Value);
        values["inboundId"]=id;values["performedByName"]=actor.Name;values["performedByUserId"]=actor.UserId;
        P(command,"@new",SqlDbType.NVarChar,JsonSerializer.Serialize(values),-1);await command.ExecuteNonQueryAsync(ct);
    }
    private static void ItemParameters(SqlCommand command,SaveReceiptItem value)
    {
        P(command,"@line",SqlDbType.BigInt,value.PoLineId);P(command,"@serial",SqlDbType.NVarChar,value.Serial,100);
        P(command,"@month",SqlDbType.TinyInt,value.ManufacturingMonth);P(command,"@year",SqlDbType.SmallInt,value.ManufacturingYear);
        P(command,"@condition",SqlDbType.VarChar,value.ReceivedCondition,8);P(command,"@note",SqlDbType.NVarChar,value.ConditionNote,1000);
        P(command,"@method",SqlDbType.VarChar,value.InputMethod,16);
    }
    private static void Actor(SqlCommand command,ReceiptActor actor)
    {P(command,"@actor",SqlDbType.Int,actor.UserId);P(command,"@name",SqlDbType.NVarChar,actor.Name,200);P(command,"@warehouse",SqlDbType.Int,actor.WarehouseId);}
    private static void P(SqlCommand command,string name,SqlDbType type,object? value,int size=0)
    {var parameter=size==0?command.Parameters.Add(name,type):command.Parameters.Add(name,type,size);parameter.Value=value??DBNull.Value;}
    private static string? Text(SqlDataReader reader,int index)=>reader.IsDBNull(index)?null:reader.GetString(index);
    private static DateTime Utc(DateTime value)=>DateTime.SpecifyKind(value,DateTimeKind.Utc);
}
