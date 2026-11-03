using Inventory.BLL.Inbound;
using Inventory.DAL.Inbound;
using Inventory.DAL.Receiving;
using Inventory.Models.Inbound;
using Microsoft.Data.SqlClient;

namespace Inventory.Inbound.Tests;

internal static class SqlTests
{
    private static readonly CancellationToken Ct=CancellationToken.None;
    private static void Check(bool value,string name)=>InboundTests.Check(value,name);
    public static async Task Run(string server)
    {
        var database="EWMS_InboundTests_"+Guid.NewGuid().ToString("N");
        var settings=new SqlConnectionStringBuilder {DataSource=server,InitialCatalog="master",IntegratedSecurity=true,
            Encrypt=false,TrustServerCertificate=true,ConnectTimeout=10};
        await using var master=new SqlConnection(settings.ConnectionString);await master.OpenAsync();
        await using(var create=new SqlCommand($"CREATE DATABASE [{database}]",master))await create.ExecuteNonQueryAsync();
        try
        {
            settings.InitialCatalog=database;
            await using var sql=new SqlConnection(settings.ConnectionString);await sql.OpenAsync();
            await Execute(sql,await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"Schema.sql")));
            var root=new DirectoryInfo(AppContext.BaseDirectory);
            while(root is not null&&!File.Exists(Path.Combine(root.FullName,"scripts/sql/migrate-inbound.sql")))root=root.Parent;
            if(root is null)throw new InvalidOperationException("Cannot locate the repository SQL fixture migration.");
            var migration=await File.ReadAllTextAsync(Path.Combine(root.FullName,"scripts/sql/migrate-inbound.sql"));
            await Execute(sql,migration);await Execute(sql,migration);
            var repo=new InboundRepository(settings.ConnectionString,InboundTests.Policy);
            var actor=InboundTests.Actor;var order=InboundTests.Order(quantity:2);
            var first=await repo.CreateAsync(order,"Batch1",actor,Ct,Guid.NewGuid());
            var header=await Header(repo,first);
            await Reject(()=>repo.CreateAsync(order with {Status="DRAFT"},null,actor,Ct),409,"unconfirmed PO");
            await Reject(()=>repo.CreateAsync(order with {WarehouseId=2},null,actor,Ct),403,"cross warehouse");
            Check(await repo.FindAsync(first,actor with {WarehouseId=2},Ct) is null,"foreign receipt hidden");
            await repo.SaveItemAsync(first,null,Item(header,"MiXeD-1"),order,actor,Ct);
            var scanned=await Header(repo,first);
            Check(scanned.RowVersion!=header.RowVersion,"item write invalidates receipt version");
            var withoutCurrent=(await repo.ProgressAsync(order,null,Ct)).Single();
            Check(withoutCurrent.CurrentQuantity==0&&withoutCurrent.ReservedQuantity==1&&withoutCurrent.RemainingQuantity==1,
                "progress without current receipt executes valid SQL and counts existing draft as reserved");
            Check(await Count(sql,"SELECT COUNT(*) FROM dbo.SerialItem")==0,"DRAFT scan never creates stock");
            await Reject(()=>repo.SaveItemAsync(first,null,Item(scanned,"MiXeD-1"),order,actor,Ct),409,"duplicate in same receipt");
            await Reject(()=>repo.SaveItemAsync(first,null,Item(header,"STALE"),order,actor,Ct),409,"stale version");
            var second=await repo.CreateAsync(order,"Batch2",actor,Ct,Guid.NewGuid());
            await Reject(async()=>await repo.SaveItemAsync(second,null,Item(await Header(repo,second),"MiXeD-1"),order,actor,Ct),409,"duplicate other DRAFT");
            var firstItem=(await repo.ItemsAsync(first,Ct)).Single();
            await repo.SaveItemAsync(first,firstItem.InboundItemId,Item(scanned,"MiXeD-1") with {ManufacturingMonth=10},order,actor,Ct);
            var edited=await Header(repo,first);
            await Execute(sql,"ALTER TABLE dbo.AuditLog ADD CONSTRAINT RejectComplete CHECK(Action<>'INBOUND_COMPLETED')");
            var failed=false;
            try{await repo.CompleteAsync(first,edited.RowVersion,order,actor,Ct);}catch(SqlException){failed=true;}
            Check(failed,"complete audit failure injected");
            Check((await Header(repo,first)).Status=="DRAFT"&&await Count(sql,"SELECT COUNT(*) FROM dbo.SerialItem")==0
                &&await Count(sql,"SELECT COUNT(*) FROM dbo.SerialMovement")==0&&await Count(sql,"SELECT COUNT(*) FROM dbo.SupplySource")==0
                &&await Count(sql,"SELECT COUNT(*) FROM dbo.InboundItem WHERE SerialItemID IS NOT NULL")==0,"complete transaction rolls back stock/source/movement/link/header");
            await Execute(sql,"ALTER TABLE dbo.AuditLog DROP CONSTRAINT RejectComplete");
            await repo.CompleteAsync(first,edited.RowVersion,order,actor,Ct);
            var done=await Header(repo,first);
            Check(done.Status=="DONE"&&done.CompletedAt is not null&&done.PerformedByUserId==actor.UserId,"completion header metadata");
            Check(await Count(sql,"SELECT COUNT(*) FROM dbo.SerialItem WHERE SerialNumber=N'MiXeD-1' AND ManufacturingMonth=10 AND ManufacturingYear=2026 AND CurrentLocationID IS NULL AND StockState='IN_WAREHOUSE'")==1,"official serial manufacturing preserved, waiting location");
            Check(await Count(sql,"SELECT COUNT(*) FROM dbo.SerialMovement WHERE EventType='PURCHASE_RECEIPT' AND ToLocationID IS NULL")==1,"one receipt movement");
            await Reject(()=>repo.CompleteAsync(first,done.RowVersion,order,actor,Ct),409,"no repeat completion");
            await Reject(()=>repo.SaveItemAsync(first,null,Item(done,"EDIT-DONE"),order,actor,Ct),409,"DONE immutable");
            await Reject(async()=>await repo.SaveItemAsync(second,null,Item(await Header(repo,second),"MiXeD-1"),order,actor,Ct),409,"existing SerialItem duplicate");
            await repo.SaveItemAsync(second,null,Item(await Header(repo,second),"Batch2-Damaged") with {ReceivedCondition="DAMAGED",ConditionNote="Broken casing"},order,actor,Ct);
            await Reject(async()=>await repo.SaveItemAsync(second,null,Item(await Header(repo,second),"OVER"),order,actor,Ct),409,"overreceiving across DONE and reserved");
            await repo.CompleteAsync(second,(await Header(repo,second)).RowVersion,order,actor,Ct);
            Check(await Count(sql,"SELECT COUNT(*) FROM dbo.SerialItem WHERE IsQuarantined=1 AND QuarantineReason=N'Broken casing'")==1,"damaged stock quarantined");
            Check(await Count(sql,"SELECT COUNT(*) FROM dbo.SupplySource WHERE ItemCondition='DAMAGED' AND EligibleQuantity=0 AND RemainingQuantity=1")==1,"damaged never eligible");
            var summary=await new ReceivingSummaryRepository(settings.ConnectionString).GetAsync(order.PurchaseOrderId,Ct);
            Check(summary.OpenInboundCount==0&&summary.Lines.Single().ReceivedQuantity==2,"PO summary counts DONE batches once");
            Check((await new ReceiptQueries(settings.ConnectionString).PendingAsync(1,Ct)).Length==2,"pending putaway reads existing stock");
            Check(await Count(sql,"SELECT COUNT(*) FROM dbo.SerialItem")==2,"pending lookup never doubles stock");
            await Reject(()=>repo.CreateAsync(order,null,actor,Ct),409,"fully received PO cannot create next batch");
            var raceOrder=InboundTests.Order(2,2);
            var a=await repo.CreateAsync(raceOrder,null,actor,Ct);var b=await repo.CreateAsync(raceOrder,null,actor,Ct);
            var ah=await Header(repo,a);var bh=await Header(repo,b);
            var races=await Task.WhenAll(new[]{a,b}.Select(async id=>
            {try{await repo.SaveItemAsync(id,null,Item(id==a?ah:bh,"RACE-SERIAL"),raceOrder,actor,Ct);return true;}catch(ReceiptException e)when(e.StatusCode==409){return false;}}));
            Check(races.Count(value=>value)==1,"concurrent same serial accepts exactly once");
            var activeId=races[0]?a:b;var active=(await repo.ItemsAsync(activeId,Ct)).Single();
            await repo.RemoveItemAsync(activeId,active.InboundItemId,(await Header(repo,activeId)).RowVersion,actor,Ct);
            Check(await Count(sql,$"SELECT COUNT(*) FROM dbo.InboundItem WHERE InboundItemID={active.InboundItemId} AND IsActive=0")==1,"remove keeps scan history");
            await repo.SaveItemAsync(a,null,Item(await Header(repo,a),"RACE-SERIAL"),raceOrder,actor,Ct);
            await repo.CancelAsync(a,(await Header(repo,a)).RowVersion,"Wrong shipment",actor,Ct);
            Check((await Header(repo,a)).Status=="CANCELLED"&&(await repo.ItemsAsync(a,Ct)).Length==0,"cancel releases scans");
            var abortToken=Guid.NewGuid();
            Check(await repo.AbortCreationAsync(raceOrder,actor,abortToken,"request failed",Ct),"failed creation fenced");
            await Reject(()=>repo.CreateAsync(raceOrder,null,actor,Ct,abortToken),409,"aborted token cannot create late draft");
            Check((await new ReceiptQueries(settings.ConnectionString).ReservationAsync(abortToken,1,Ct)).Terminal,"aborted creation gives terminal release proof");
            var singlePolicy=new InboundRepository(settings.ConnectionString,new(false,false,false));
            var singleOrder=InboundTests.Order(3,1);
            var only=await singlePolicy.CreateAsync(singleOrder,null,actor,Ct);
            await Reject(()=>singlePolicy.CreateAsync(singleOrder,null,actor,Ct),409,"single open draft policy enforced under transaction lock");
            await Reject(async()=>await singlePolicy.CancelAsync(only,(await Header(singlePolicy,only)).RowVersion,"Not allowed",actor,Ct),403,"whole receipt cancellation policy");
            Check(await Count(sql,"SELECT COUNT(*) FROM dbo.AuditLog WHERE Action='INBOUND_SERIAL_ADDED'")>=3
                &&await Count(sql,"SELECT COUNT(*) FROM dbo.AuditLog WHERE Action='INBOUND_COMPLETED'")==2,"business actions auditable");
            Console.WriteLine("PASS SQL Inbound: migration twice, multi-batch, duplicate/race, quantity reservation, immutable DONE, rollback, quarantine, pending stock, audit, abort fence.");
        }
        finally
        {
            if(!database.StartsWith("EWMS_InboundTests_",StringComparison.Ordinal)||database.Length!=50)throw new Exception("Unsafe database cleanup target.");
            SqlConnection.ClearAllPools();
            await using var cleanup=new SqlCommand($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]",master);
            await cleanup.ExecuteNonQueryAsync();
        }
    }
    private static async Task<ReceiptHeader> Header(InboundRepository repo,long id)=>await repo.FindAsync(id,InboundTests.Actor,Ct)??throw new Exception("Missing receipt");
    private static SaveReceiptItem Item(ReceiptHeader header,string serial)=>InboundTests.Valid with{Serial=serial,RowVersion=header.RowVersion,ManufacturingMonth=1};
    private static async Task Reject(Func<Task> action,int expected,string name)
    {try{await action();}catch(ReceiptException e)when(e.StatusCode==expected){return;}throw new Exception("FAIL expected SQL rejection: "+name);}
    private static async Task Execute(SqlConnection sql,string statement){await using var cmd=new SqlCommand(statement,sql);await cmd.ExecuteNonQueryAsync();}
    private static async Task<int> Count(SqlConnection sql,string statement){await using var cmd=new SqlCommand(statement,sql);return Convert.ToInt32(await cmd.ExecuteScalarAsync());}
}
