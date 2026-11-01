using Microsoft.Data.SqlClient;
using WMS.Audit;

namespace WMS.Audit.Source.Tests;

internal static class SqlReaderTests
{
    public static async Task Run(string server)
    {
        var database = "EWMS_AuditTests_" + Guid.NewGuid().ToString("N");
        var settings = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 8 };
        await using var master = new SqlConnection(settings.ConnectionString);
        await master.OpenAsync();
        await using (var create = new SqlCommand($"CREATE DATABASE [{database}]", master)) await create.ExecuteNonQueryAsync();
        try
        {
            settings.InitialCatalog = database;
            await using var connection = new SqlConnection(settings.ConnectionString);
            await connection.OpenAsync();
            await using (var schema = new SqlCommand("""
                CREATE TABLE dbo.PurchaseOrder (PurchaseOrderID bigint PRIMARY KEY, POCode varchar(40));
                CREATE TABLE dbo.Inbound (InboundID bigint PRIMARY KEY, ReceiptCode varchar(40));
                CREATE TABLE dbo.AuditLog (AuditLogID bigint PRIMARY KEY, UserID int NOT NULL, Action varchar(60) NOT NULL,
                    EntityName varchar(100) NOT NULL, EntityID nvarchar(100) NOT NULL, OldValues nvarchar(max) NULL,
                    NewValues nvarchar(max) NULL, Status varchar(20) NOT NULL, CreatedAt datetime2(3) NOT NULL);
                INSERT dbo.PurchaseOrder VALUES (10,'PO-100%_X'),(11,'PO-OTHER');
                INSERT dbo.Inbound VALUES (20,'IN-20');
                INSERT dbo.AuditLog VALUES
                (1,2,'CREATE_PO','PurchaseOrder','10',NULL,N'{"createdByName":"Creator Snapshot","password":"hidden"}','SUCCESS','2026-11-01T01:00:00'),
                (2,3,'UPDATE_PO','PurchaseOrder','10',N'{"quantity":1}',N'{"createdByName":"Original Creator","quantity":2}','SUCCESS','2026-11-01T02:00:00'),
                (3,3,'CONFIRM_PO','PurchaseOrder','11',NULL,N'broken password=hidden','SUCCESS','2026-11-01T02:00:00'),
                (4,4,'CREATE_INBOUND','Inbound','20',NULL,N'{"createdByName":"Inbound Creator"}','SUCCESS','2026-11-01T03:00:00'),
                (5,4,'COMPLETE_INBOUND','Inbound','20',NULL,N'{"performedByName":"Warehouse Worker"}','SUCCESS','2026-11-01T04:00:00'),
                (6,4,'CONFIRM_PUTAWAY','Putaway','20',NULL,N'{"locationId":12}','SUCCESS','2026-11-01T05:00:00'),
                (7,4,'CONFIRM_PUTAWAY','Putaway','999',NULL,N'{"inboundId":20,"locationId":13}','SUCCESS','2026-11-01T05:00:00'),
                (8,4,'MOVE_SERIAL','SerialMovement','20',NULL,N'{}','SUCCESS','2026-11-01T06:00:00'),
                (9,2,'CREATE_PO','PurchaseOrder','TEMP',NULL,N'{"poCode":"PO-TEMP"}','FAILED','2026-11-01T02:00:00');
                """, connection)) await schema.ExecuteNonQueryAsync();
            var procurement = new SqlAuditReader(settings.ConnectionString, "procurement");
            var inventory = new SqlAuditReader(settings.ConnectionString, "inventory");
            var ct = CancellationToken.None;
            var po = await procurement.PageAsync(new(), ct);
            SourceTests.Check(po.TotalCount == 4 && po.Items.Length == 4, "PO source scoped count");
            SourceTests.Check((await inventory.PageAsync(new(), ct)).TotalCount == 4, "Inventory excludes SerialMovement");
            var user = await procurement.PageAsync(new() { UserId = 3, Action = "UPDATE_PO" }, ct);
            SourceTests.Check(user.TotalCount == 1 && user.Items[0].AuditLogId == 2, "user and action filters");
            SourceTests.Check(user.Items[0].ActorName is null, "editor not confused with original creator");
            SourceTests.Check((await procurement.PageAsync(new() { DocumentCode = "%_" }, ct)).TotalCount == 2, "document wildcard characters escaped literally");
            SourceTests.Check((await inventory.PageAsync(new() { BusinessGroup = "PUTAWAY" }, ct)).TotalCount == 2, "putaway group");
            var dates = await procurement.PageAsync(new() { From = DateTimeOffset.Parse("2026-11-01T09:00:00+07:00"), To = DateTimeOffset.Parse("2026-11-01T10:00:00+07:00") }, ct);
            SourceTests.Check(dates.TotalCount == 3, "UTC+7 inclusive start/exclusive end");
            SourceTests.Check((await inventory.DetailAsync(6, ct))!.Item.DocumentCode == "20", "no unrelated inbound receipt from ID collision");
            SourceTests.Check((await inventory.DetailAsync(7, ct))!.Item.DocumentCode == "IN-20", "explicit inboundId resolves receipt");
            SourceTests.Check(!(await procurement.DetailAsync(1, ct))!.NewValues!.Contains("hidden"), "SQL source detail sanitized");
            SourceTests.Check(!(await procurement.DetailAsync(3, ct))!.NewValues!.Contains("password=hidden"), "malformed SQL payload hidden");
            SourceTests.Check(await inventory.DetailAsync(8, ct) is null, "SerialMovement excluded from detail");
            var ids = new List<long>();
            string? cursor = null;
            do
            {
                var page = await procurement.PageAsync(new() { PageSize = 1, Cursor = cursor }, ct);
                ids.AddRange(page.Items.Select(item => item.AuditLogId));
                cursor = page.NextCursor;
            } while (cursor is not null);
            SourceTests.Check(ids.SequenceEqual(new long[] { 9, 3, 2, 1 }), "stable keyset pagination at timestamp ties");
            var boundary = AuditCursor.Encode(new(DateTimeOffset.Parse("2026-11-01T02:00:00Z"), "inventory", 1));
            SourceTests.Check((await procurement.PageAsync(new() { Cursor = boundary }, ct)).Items.Length == 4, "cross-source cursor tie boundary");
            Console.WriteLine("PASS SQL: source filters, UTC dates, wildcard literal, actor/document mapping, JSON redaction and tied cursor pagination. Disposable database only.");
        }
        finally
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(database, "^EWMS_AuditTests_[a-f0-9]{32}$")) throw new InvalidOperationException("Unsafe test cleanup target.");
            SqlConnection.ClearAllPools();
            await using var cleanup = new SqlCommand($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]", master);
            await cleanup.ExecuteNonQueryAsync();
        }
    }
}
