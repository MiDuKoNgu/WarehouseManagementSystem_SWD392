using System.Data;
using Inventory.BLL.Putaway;
using Inventory.DAL.Putaway;
using Inventory.Models.Putaway;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Inbound.Tests;

/// <summary>All writes and failure-injection DDL run only in three GUID-named disposable fixture databases.</summary>
internal static class PutawaySqlTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly PutawayActor ActorA = new(22, "Nhân viên A", 1);
    private static readonly PutawayActor ActorB = new(23, "Nhân viên B", 2);
    private static int checks;
    private static void Check(bool condition, string name)
    {
        checks++;
        InboundTests.Check(condition, "Putaway SQL " + name);
    }

    public static async Task Run(string server)
    {
        var prefix = "EWMS_PutawayTests_" + Guid.NewGuid().ToString("N");
        var databases = new[] { prefix + "_Inventory", prefix + "_Catalog", prefix + "_Warehouse" };
        var settings = new SqlConnectionStringBuilder
        {
            DataSource = server, InitialCatalog = "master", IntegratedSecurity = true,
            Encrypt = false, TrustServerCertificate = true, ConnectTimeout = 10
        };
        await using var master = new SqlConnection(settings.ConnectionString);
        await master.OpenAsync(Ct);
        try
        {
            foreach (var database in databases) await Execute(master, $"CREATE DATABASE [{database}];");
            settings.InitialCatalog = databases[0];
            await using var inventory = new SqlConnection(settings.ConnectionString);
            await inventory.OpenAsync(Ct);
            await Execute(inventory, InventorySchema);
            var fixture = new Fixture(settings.ConnectionString, databases[1], databases[2]);
            settings.InitialCatalog = databases[1];
            await using (var catalog = new SqlConnection(settings.ConnectionString))
            {
                await catalog.OpenAsync(Ct);
                await Execute(catalog, CatalogSchema);
            }
            settings.InitialCatalog = databases[2];
            await using (var warehouse = new SqlConnection(settings.ConnectionString))
            {
                await warehouse.OpenAsync(Ct);
                await Execute(warehouse, WarehouseSchema);
            }
            await fixture.Seed();
            await ListingAndProductRules(fixture);
            await CommitAndInvariants(fixture);
            await CapacityAndRaces(fixture);
            await StateAndPolicyRejections(fixture);
            await AtomicRollback(fixture);
            Console.WriteLine($"PASS Putaway SQL: {checks} assertions on disposable databases (two warehouses, six profiles, concurrency, rollback, invariants).");
        }
        finally
        {
            SqlConnection.ClearAllPools();
            foreach (var database in databases.Reverse())
            {
                if (!database.StartsWith("EWMS_PutawayTests_", StringComparison.Ordinal) || !database.StartsWith(prefix, StringComparison.Ordinal))
                    throw new InvalidOperationException("Refusing to remove a database outside the generated fixture.");
                await Execute(master, $"IF DB_ID(N'{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END;");
            }
        }
    }

    private static async Task ListingAndProductRules(Fixture f)
    {
        foreach (var actor in new[] { ActorA, ActorB })
        {
            var page = await f.Repository.PendingAsync(actor, null, 1, 100, Ct);
            Check(page.TotalCount == 6 && page.Items.All(x => x.CurrentWarehouseId == actor.WarehouseId), "pending isolated to assigned warehouse");
            var offset = (actor.WarehouseId - 1) * 1000;
            for (var product = 1; product <= 6; product++)
            {
                var id = actor.WarehouseId * 100 + product;
                var suggestions = await f.Repository.SuggestionsAsync(actor, id, Ct);
                var expected = offset + (product <= 2 ? 12 : product <= 4 ? 23 : 33);
                Check(suggestions.Candidates[0].LocationId == expected, $"warehouse {actor.WarehouseId}, product {product} STANDARD mapping");
                Check(suggestions.Candidates.Any(x => x.LocationId == offset + 3 && x.IsReserve), "RESERVE offered while STANDARD still has room");
                Check(suggestions.Serial.Product.ProductName != "" && suggestions.Serial.Product.CategoryName != "" && suggestions.Serial.Product.BrandName != "",
                    "authoritative named storage profile");
            }
            var firstPage = await f.Repository.PendingAsync(actor, null, 1, 2, Ct);
            var secondPage = await f.Repository.PendingAsync(actor, null, 2, 2, Ct);
            Check(firstPage.Items.Length == 2 && secondPage.Items.Length == 2 && !firstPage.Items.Select(x => x.SerialItemId)
                .Intersect(secondPage.Items.Select(x => x.SerialItemId)).Any(), "paged pending listing");
        }
        await Reject(() => f.Repository.FindAsync(ActorA, 201, null, Ct), "SERIAL_NOT_FOUND");
        await f.Insert(390, 7, 1, serialNumber: "literal%_serial");
        Check((await f.Repository.PendingAsync(ActorA, "%_", 1, 100, Ct)).Items.Single().SerialItemId == 390,
            "LIKE metacharacters treated as literal text");
        Check((await f.Repository.PendingAsync(ActorA, "%' OR 1=1--", 1, 100, Ct)).TotalCount == 0,
            "parameterized search cannot inject SQL");
        Check((await f.Repository.FindAsync(ActorA, null, "literal%_serial", Ct)).SerialItemId == 390,
            "exact scan resolves one registered serial");
    }

    private static async Task CommitAndInvariants(Fixture f)
    {
        var before = await f.Scalar<string>("SELECT CONCAT(SerialNumber,'|',ProductID,'|',StockState,'|',ItemCondition,'|',IsQuarantined,'|',CurrentWarehouseID,'|',OriginalInboundID,'|',OriginalAdjustmentID,'|',CurrentSupplySourceID) FROM dbo.SerialItem WHERE SerialItemID=101");
        var unchanged = await f.Scalar<string>("SELECT CONCAT((SELECT SUM(RemainingQuantity) FROM dbo.SupplySource),'|',(SELECT SUM(EligibleQuantity) FROM dbo.SupplySource),'|',(SELECT SUM(ReceivedQuantity) FROM dbo.Inbound),'|',(SELECT COUNT(*) FROM dbo.SerialItem))");
        var count = (await f.Repository.PendingAsync(ActorA, null, 1, 100, Ct)).TotalCount;
        var version = (await f.Find(101)).RowVersion;
        var result = await f.Repository.ConfirmAsync(ActorA, new(101, 12, version), Ct);
        Check(result.TargetLocationId == 12 && result.ActorUserId == 22 && result.ActorName == ActorA.Name && result.LocationPath.Contains("B"), "write result actor/path");
        Check(result.RowVersion != version, "serial rowversion changes");
        Check((await f.Repository.PendingAsync(ActorA, null, 1, 100, Ct)).TotalCount == count - 1, "pending decreases by exactly one");
        Check((await f.Find(101)).CurrentLocationPath == result.LocationPath, "reload current location path");
        Check(before == await f.Scalar<string>("SELECT CONCAT(SerialNumber,'|',ProductID,'|',StockState,'|',ItemCondition,'|',IsQuarantined,'|',CurrentWarehouseID,'|',OriginalInboundID,'|',OriginalAdjustmentID,'|',CurrentSupplySourceID) FROM dbo.SerialItem WHERE SerialItemID=101"), "only current location and rowversion change");
        Check(unchanged == await f.Scalar<string>("SELECT CONCAT((SELECT SUM(RemainingQuantity) FROM dbo.SupplySource),'|',(SELECT SUM(EligibleQuantity) FROM dbo.SupplySource),'|',(SELECT SUM(ReceivedQuantity) FROM dbo.Inbound),'|',(SELECT COUNT(*) FROM dbo.SerialItem))"), "onhand, supply quantities, inbound quantities unchanged");
        Check(await f.Scalar<int>("SELECT COUNT(*) FROM dbo.SerialMovement WHERE SerialItemID=101 AND EventType='PUTAWAY' AND PerformedByUserID=22 AND FromLocationID IS NULL AND ToLocationID=12 AND FromWarehouseID=1 AND ToWarehouseID=1 AND FromState=ToState AND FromCondition=ToCondition AND FromQuarantined=ToQuarantined") == 1,
            "movement retains all before/after state and actor");
        Check(await f.Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityID='101' AND Action='PUTAWAY_COMPLETED' AND UserID=22 AND JSON_VALUE(NewValues,'$.performedByName')=N'Nhân viên A'") == 1,
            "transactional audit actor and serial");
        await Reject(() => f.Repository.ConfirmAsync(ActorA, new(101, 3, version), Ct), "ALREADY_LOCATED");
        Check(await f.Scalar<int>("SELECT COUNT(*) FROM dbo.SerialMovement WHERE SerialItemID=101") == 1, "retry cannot relocate or create second movement");

        // A staff member may deliberately choose reserve although the model's standard location is empty.
        Check((await f.Repository.SuggestionsAsync(ActorA, 102, Ct)).Candidates.Any(x => !x.IsReserve), "manual reserve test has STANDARD available");
        await f.Confirm(102, 3);
        Check((await f.Find(102)).CurrentLocationId == 3, "manual reserve succeeds");
        await f.Insert(300, 7, 1, "DAMAGED", true);
        await Reject(() => f.Confirm(300, 6), "LOCATION_UNAVAILABLE");
        Check((await f.Repository.SuggestionsAsync(ActorA, 300, Ct)).Candidates.All(x => x.LocationId == 4), "quarantined DAMAGED suggestions only isolation slot");
        await f.Confirm(300, 4);
        Check((await f.Find(300)).IsQuarantined && (await f.Find(300)).ItemCondition == "DAMAGED", "damaged quarantine preserved on commit");
        Check(await f.Scalar<int>("SELECT COUNT(*) FROM dbo.SerialMovement WHERE SerialItemID=300 AND FromQuarantined=1 AND ToQuarantined=1 AND FromCondition='DAMAGED' AND ToCondition='DAMAGED'") == 1,
            "damaged movement preserves quarantine");
        await f.Confirm(201, 1012, ActorB);
        Check((await f.Find(201, ActorB)).CurrentWarehouseId == 2 && (await f.Find(201, ActorB)).CurrentLocationId == 1012, "warehouse B independent commit");
    }

    private static async Task CapacityAndRaces(Fixture f)
    {
        for (var id = 301; id <= 304; id++)
        {
            await f.Insert(id, 7, 1);
            await f.Confirm(id, 6);
        }
        Check(await f.Scalar<int>("SELECT COUNT(*) FROM dbo.SerialItem WHERE CurrentLocationID=6 AND StockState='IN_WAREHOUSE'") == 4,
            "multiple serials occupy one floor slot without stacking");
        await f.Insert(305, 7, 1);
        await Reject(() => f.Confirm(305, 6), "LOCATION_UNAVAILABLE");
        var reserveSnapshot = await f.Repository.SuggestionsAsync(ActorA, 305, Ct);
        Check(reserveSnapshot.Candidates.All(x => x.LocationId != 6), "capacity updates after commits");

        await f.Insert(310, 7, 1);
        var same = new ConfirmPutaway(310, 7, (await f.Find(310)).RowVersion);
        var duplicate = await Task.WhenAll(Attempt(() => f.Repository.ConfirmAsync(ActorA, same, Ct)),
            Attempt(() => f.Repository.ConfirmAsync(ActorA, same, Ct)));
        Check(duplicate.Count(x => x is null) == 1 && duplicate.Count(x => x is PutawayException { StatusCode: 409 }) == 1,
            "simultaneous same serial commits exactly once");
        Check(await f.Scalar<int>("SELECT COUNT(*) FROM dbo.SerialMovement WHERE SerialItemID=310") == 1, "same-serial concurrency has one movement");

        await f.Insert(311, 7, 1);
        await f.Insert(312, 7, 1);
        var first = new ConfirmPutaway(311, 5, (await f.Find(311)).RowVersion);
        var second = new ConfirmPutaway(312, 5, (await f.Find(312)).RowVersion);
        var capacity = await Task.WhenAll(Attempt(() => f.Repository.ConfirmAsync(ActorA, first, Ct)),
            Attempt(() => f.Repository.ConfirmAsync(ActorA, second, Ct)));
        Check(capacity.Count(x => x is null) == 1 && capacity.Count(x => x is PutawayException { StatusCode: 409 }) == 1,
            "parallel serials race for final floor capacity without overbooking");
        Check(await f.Scalar<int>("SELECT COUNT(*) FROM dbo.SerialItem WHERE CurrentLocationID=5") == 1,
            "capacity race leaves one occupant");
        Check(await f.Scalar<int>("SELECT COUNT(*) FROM dbo.SerialMovement WHERE SerialItemID IN (311,312)") == 1,
            "capacity loser has no movement");

        await f.Insert(313, 8, 1);
        await f.Insert(314, 8, 1);
        await f.Confirm(313, 8);
        var impossible = await Attempt(() => f.Confirm(314, 8));
        Check(impossible is PutawayException { Code: "LOCATION_UNAVAILABLE" } error && error.Message.Contains("2D"),
            "SQL revalidation rejects geometry even when total area fits");

        await f.Insert(360, 9, 1);
        await f.Insert(361, 9, 1);
        await f.Insert(362, 9, 1);
        await f.Confirm(360, 23);
        var tier = (await f.Repository.SuggestionsAsync(ActorA, 361, Ct)).Candidates.Single(x => x.LocationId == 23);
        Check(tier.UsedVolumeM3 == 2m && tier.AvailableVolumeM3 == 2.455m && tier.AvailableAreaM2 is null,
            "SQL authoritative tier occupancy has m3 units");
        await f.Confirm(361, 23);
        await Reject(() => f.Confirm(362, 23), "LOCATION_UNAVAILABLE");
        Check(await f.Scalar<int>("SELECT COUNT(*) FROM dbo.SerialItem WHERE CurrentLocationID=23") == 2,
            "tier cumulative volume rechecked on commit");
        await f.Insert(363, 10, 1);
        await f.Insert(364, 10, 1);
        await f.Confirm(363, 33);
        await Reject(() => f.Confirm(364, 33), "LOCATION_UNAVAILABLE");
        Check(await f.Scalar<int>("SELECT COUNT(*) FROM dbo.SerialMovement WHERE SerialItemID=364") == 0,
            "tier cumulative weight rejects without movement even when volume fits");

        for (var id = 365; id <= 367; id++)
        {
            await f.Insert(id, 1, 1);
            await f.Confirm(id, 12);
        }
        await f.Insert(368, 1, 1);
        var fallback = await f.Repository.SuggestionsAsync(ActorA, 368, Ct);
        Check(fallback.Candidates.Length > 0 && fallback.Candidates.All(x => x.IsReserve),
            "full STANDARD produces only valid RESERVE fallback");
    }

    private static async Task StateAndPolicyRejections(Fixture f)
    {
        await f.Insert(320, 7, 1);
        var stale = new ConfirmPutaway(320, 9, (await f.Find(320)).RowVersion);
        await f.Execute("UPDATE dbo.SerialItem SET IsQuarantined=1 WHERE SerialItemID=320");
        await Reject(() => f.Repository.ConfirmAsync(ActorA, stale, Ct), "STALE_SERIAL");
        Check((await f.Find(320)).CurrentLocationId is null, "stale rowversion makes no write");
        await f.Insert(321, 7, 1);
        var changed = new ConfirmPutaway(321, 9, (await f.Find(321)).RowVersion);
        await f.Execute("UPDATE dbo.SerialItem SET StockState='SOLD' WHERE SerialItemID=321");
        await Reject(() => f.Repository.ConfirmAsync(ActorA, changed, Ct), "SERIAL_UNAVAILABLE");
        await f.Insert(322, 7, 1);
        await Reject(() => f.Confirm(322, 1003), "LOCATION_NOT_FOUND");
        await Reject(() => f.Confirm(322, 4), "LOCATION_UNAVAILABLE");
        await Reject(() => f.Confirm(322, 12), "LOCATION_UNAVAILABLE");
        await Reject(() => f.Confirm(322, 2), "LOCATION_UNAVAILABLE");
        await f.WarehouseExecute("UPDATE dbo.StorageLocation SET Status='INACTIVE' WHERE LocationID=2");
        await Reject(() => f.Confirm(322, 9), "LOCATION_UNAVAILABLE");
        await f.WarehouseExecute("UPDATE dbo.StorageLocation SET Status='ACTIVE' WHERE LocationID=2");
        await f.WarehouseExecute("UPDATE dbo.StorageLocation SET ParentLocationID=99999 WHERE LocationID=9");
        await Reject(() => f.Confirm(322, 9), "LOCATION_UNAVAILABLE");
        await f.WarehouseExecute("UPDATE dbo.StorageLocation SET ParentLocationID=2 WHERE LocationID=9");
        await f.Insert(323, 7, 1, inboundId: 3);
        await Reject(() => f.Confirm(323, 9), "RECEIPT_NOT_DONE");
        Check((await f.Repository.PendingAsync(ActorA, "SERIAL-323", 1, 100, Ct)).TotalCount == 0, "draft receipt excluded from pending");
        await f.Execute("UPDATE dbo.Inbound SET Status='DONE',CompletedAt=NULL WHERE InboundID=3");
        await Reject(() => f.Confirm(323, 9), "RECEIPT_NOT_DONE");
        Check((await f.Repository.PendingAsync(ActorA, "SERIAL-323", 1, 100, Ct)).TotalCount == 0, "DONE without completion proof excluded");
        await f.Execute("UPDATE dbo.Inbound SET Status='COMPLETED',CompletedAt=SYSUTCDATETIME() WHERE InboundID=3");
        Check((await f.Repository.SuggestionsAsync(ActorA, 323, Ct)).Candidates.Length > 0, "legacy COMPLETED normalizes to official DONE");
        await f.Execute("UPDATE dbo.Inbound SET Status='DRAFT',CompletedAt=NULL WHERE InboundID=3");
        await f.Insert(324, 7, 1, inboundId: null, origin: "ADJUSTMENT");
        Check((await f.Repository.PendingAsync(ActorA, "SERIAL-324", 1, 100, Ct)).TotalCount == 1,
            "official serial without original inbound is not lost to inner join");
        await f.Insert(325, 7, 1, inboundId: null);
        Check((await f.Repository.PendingAsync(ActorA, "SERIAL-325", 1, 100, Ct)).TotalCount == 0, "purchase missing receipt excluded from pending");
        await Reject(() => f.Confirm(325, 9), "RECEIPT_NOT_DONE");
        await f.Execute("CREATE TABLE dbo.Stocktake (StocktakeID int NOT NULL);");
        try { await Reject(() => f.Confirm(322, 9), "MOVEMENT_GUARD_REQUIRED"); }
        finally { await f.Execute("DROP TABLE dbo.Stocktake;"); }
        await f.WarehouseExecute("UPDATE dbo.Warehouse SET WarehouseType='RENTED',RentalEndDate=DATEADD(day,-1,CONVERT(date,SYSUTCDATETIME())) WHERE WarehouseID=1");
        try { await Reject(() => f.Confirm(322, 9), "RENTAL_POLICY_REQUIRED"); }
        finally { await f.WarehouseExecute("UPDATE dbo.Warehouse SET WarehouseType='OWNED',RentalEndDate=NULL WHERE WarehouseID=1"); }
        Check(await f.Scalar<int>("SELECT COUNT(*) FROM dbo.SerialMovement WHERE SerialItemID IN(320,321,322,323,325)") == 0,
            "rejected operations produce no movement");
    }

    private static async Task AtomicRollback(Fixture f)
    {
        foreach (var audit in new[] { false, true })
        {
            var id = audit ? 331 : 330;
            await f.Insert(id, 7, 1);
            var version = (await f.Find(id)).RowVersion;
            var table = audit ? "AuditLog" : "SerialMovement";
            var actionColumn = audit ? "Action" : "EventType";
            var rejectedAction = audit ? "PUTAWAY_COMPLETED" : "PUTAWAY";
            var movementCount = await f.Scalar<int>("SELECT COUNT(*) FROM dbo.SerialMovement");
            var auditCount = await f.Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog");
            // CHECK constraints reject the intended write without introducing triggers
            // incompatible with EF's normal SQL Server OUTPUT inserts.
            await f.Execute($"ALTER TABLE dbo.{table} WITH NOCHECK ADD CONSTRAINT RejectPutawayFixture CHECK ({actionColumn}<>'{rejectedAction}');");
            try
            {
                var error = await Attempt(() => f.Repository.ConfirmAsync(ActorA, new(id, 9, version), Ct));
                Check(error is SqlException { Number: 547 }
                    or DbUpdateException { InnerException: SqlException { Number: 547 } },
                    "failure injected at " + table + "; actual: " + error);
            }
            finally { await f.Execute($"ALTER TABLE dbo.{table} DROP CONSTRAINT RejectPutawayFixture;"); }
            var item = await f.Find(id);
            Check(item.CurrentLocationId is null && item.RowVersion == version, "serial rolls back with " + table + " failure");
            Check(await f.Scalar<int>("SELECT COUNT(*) FROM dbo.SerialMovement") == movementCount &&
                await f.Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog") == auditCount, "movement and audit atomically roll back");
        }
    }

    private static async Task<Exception?> Attempt(Func<Task> action)
    {
        try { await action(); return null; }
        catch (Exception error) { return error; }
    }
    private static async Task Reject(Func<Task> action, string code)
    {
        var error = await Attempt(action);
        Check(error is PutawayException expected && expected.Code == code,
            $"expected {code}; got {error?.GetType().Name}: {error?.Message}");
    }
    private static async Task Execute(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 60 };
        await command.ExecuteNonQueryAsync(Ct);
    }

    private sealed class Fixture(string connectionString, string catalogDatabase, string warehouseDatabase)
    {
        public SqlPutawayRepository Repository { get; } = new(connectionString, new CurrentSchemaPutawayGuard(), catalogDatabase, warehouseDatabase);
        public Task<PutawaySerial> Find(long id, PutawayActor? actor = null) => Repository.FindAsync(actor ?? ActorA, id, null, Ct);
        public async Task<PutawayResult> Confirm(long id, int locationId, PutawayActor? actor = null)
        {
            actor ??= ActorA;
            return await Repository.ConfirmAsync(actor, new(id, locationId, (await Find(id, actor)).RowVersion), Ct);
        }
        public async Task Execute(string sql)
        {
            await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(Ct);
            await PutawaySqlTests.Execute(connection, sql);
        }
        public async Task WarehouseExecute(string sql)
        {
            var settings = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = warehouseDatabase };
            await using var connection = new SqlConnection(settings.ConnectionString); await connection.OpenAsync(Ct);
            await PutawaySqlTests.Execute(connection, sql);
        }
        public async Task<T> Scalar<T>(string sql)
        {
            await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(Ct);
            await using var command = new SqlCommand(sql, connection);
            return (T)(await command.ExecuteScalarAsync(Ct))!;
        }
        public async Task Insert(long id, int productId, int warehouseId, string condition = "GOOD", bool quarantine = false,
            string? serialNumber = null, long? inboundId = -1, string origin = "PURCHASE")
        {
            await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(Ct);
            await using var command = new SqlCommand("""
                INSERT dbo.SerialItem(SerialItemID,SerialNumber,ProductID,StockState,ItemCondition,IsQuarantined,CurrentWarehouseID,
                    CurrentLocationID,OriginalInboundID,OriginalAdjustmentID,CurrentSupplySourceID,RegisteredAt,OriginType)
                VALUES(@id,@serial,@product,'IN_WAREHOUSE',@condition,@quarantine,@warehouse,NULL,@inbound,NULL,1,SYSUTCDATETIME(),@origin)
                """, connection);
            command.Parameters.Add("@id", SqlDbType.BigInt).Value = id;
            command.Parameters.Add("@serial", SqlDbType.NVarChar, 100).Value = serialNumber ?? $"SERIAL-{id}";
            command.Parameters.Add("@product", SqlDbType.Int).Value = productId;
            command.Parameters.Add("@condition", SqlDbType.VarChar, 8).Value = condition;
            command.Parameters.Add("@quarantine", SqlDbType.Bit).Value = quarantine;
            command.Parameters.Add("@warehouse", SqlDbType.Int).Value = warehouseId;
            command.Parameters.Add("@inbound", SqlDbType.BigInt).Value = inboundId == -1 ? warehouseId : (object?)inboundId ?? DBNull.Value;
            command.Parameters.Add("@origin", SqlDbType.VarChar, 20).Value = origin;
            await command.ExecuteNonQueryAsync(Ct);
        }
        public async Task Seed()
        {
            await Execute("INSERT dbo.Inbound VALUES(1,'PN-1','DONE',SYSUTCDATETIME(),10),(2,'PN-2','DONE',SYSUTCDATETIME(),10),(3,'PN-DRAFT','DRAFT',NULL,0); INSERT dbo.SupplySource VALUES(1,20,20);");
            for (var warehouse = 1; warehouse <= 2; warehouse++)
                for (var product = 1; product <= 6; product++) await Insert(warehouse * 100 + product, product, warehouse);
        }
    }

    private const string InventorySchema = """
        CREATE TABLE dbo.Inbound(InboundID bigint PRIMARY KEY,ReceiptCode nvarchar(100),Status varchar(20),CompletedAt datetime2 NULL,ReceivedQuantity int);
        CREATE TABLE dbo.SupplySource(SupplySourceID bigint PRIMARY KEY,RemainingQuantity int,EligibleQuantity int);
        CREATE TABLE dbo.SerialItem(SerialItemID bigint PRIMARY KEY,SerialNumber nvarchar(100) NOT NULL UNIQUE,ProductID int NOT NULL,
            StockState varchar(20) NOT NULL,ItemCondition varchar(8) NOT NULL,IsQuarantined bit NOT NULL,CurrentWarehouseID int NULL,
            CurrentLocationID int NULL,OriginalInboundID bigint NULL,OriginalAdjustmentID bigint NULL,CurrentSupplySourceID bigint NULL,
            RegisteredAt datetime2 NOT NULL,OriginType varchar(20) NOT NULL,RowVersion rowversion NOT NULL);
        CREATE TABLE dbo.SerialMovement(MovementID bigint IDENTITY PRIMARY KEY,SerialItemID bigint NOT NULL,PerformedByUserID int NOT NULL,
            EventType varchar(30) NOT NULL,OccurredAt datetime2 NOT NULL,FromState varchar(20),ToState varchar(20),FromCondition varchar(8),
            ToCondition varchar(8),FromWarehouseID int,ToWarehouseID int,FromLocationID int NULL,ToLocationID int NULL,
            FromQuarantined bit,ToQuarantined bit,Note nvarchar(1000),
            InboundID bigint NULL,OutboundID bigint NULL,ReturnReceiptID bigint NULL,DispatchID bigint NULL,
            TransferReceiptID bigint NULL,AdjustmentID bigint NULL,LossRequestID bigint NULL);
        CREATE TABLE dbo.AuditLog(AuditID bigint IDENTITY PRIMARY KEY,UserID int,Action nvarchar(100),EntityName nvarchar(100),
            EntityID nvarchar(100),OldValues nvarchar(max),NewValues nvarchar(max),Status varchar(20),CreatedAt datetime2);
        """;

    private const string CatalogSchema = """
        CREATE TABLE dbo.Category(CategoryID int PRIMARY KEY,CategoryName nvarchar(100));
        CREATE TABLE dbo.Brand(BrandID int PRIMARY KEY,BrandName nvarchar(100));
        CREATE TABLE dbo.Product(ProductID int PRIMARY KEY,ProductCode nvarchar(100),ProductName nvarchar(200),CategoryID int,BrandID int,
            Status varchar(20),LengthCm decimal(10,2),WidthCm decimal(10,2),HeightCm decimal(10,2),GrossWeightKg decimal(10,2));
        INSERT dbo.Category VALUES(9,N'Điều hòa'),(10,N'Ti vi'),(11,N'Tủ lạnh'),(12,N'Bếp'),(13,N'Nồi cơm'),(99,N'Thiết bị thử');
        INSERT dbo.Brand VALUES(2,N'Samsung'),(8,N'Daikin'),(9,N'Panasonic'),(10,N'Electrolux'),(11,N'Sharp');
        INSERT dbo.Product VALUES
            (1,'TV-SAMSUNG',N'Ti vi Samsung 55 inch',10,2,'ACTIVE',120,25,75,20),
            (2,'FRIDGE-PANA',N'Tủ lạnh Panasonic 300 lít',11,9,'ACTIVE',60,65,175,60),
            (3,'AC-DAIKIN',N'Điều hòa Daikin 1 HP',9,8,'ACTIVE',100,30,40,15),
            (4,'AC-PANA',N'Điều hòa Panasonic 1.5 HP',9,9,'ACTIVE',100,30,40,15),
            (5,'HOB-ELECT',N'Bếp từ đôi Electrolux',12,10,'ACTIVE',80,50,20,10),
            (6,'RICE-SHARP',N'Nồi cơm điện Sharp 1.8 lít',13,11,'ACTIVE',40,40,45,5),
            (7,'FLOOR-TEST',N'Kiện thử cạnh nhau',99,2,'ACTIVE',100,60,100,25),
            (8,'GEOMETRY-TEST',N'Kiện thử hình học',99,2,'ACTIVE',121,61,100,25),
            (9,'TIER-VOLUME',N'Kiện thử thể tích tầng',9,8,'ACTIVE',200,100,100,100),
            (10,'TIER-WEIGHT',N'Kiện thử tải trọng tầng',12,10,'ACTIVE',100,50,50,600);
        """;

    private const string WarehouseSchema = """
        CREATE TABLE dbo.Warehouse(WarehouseID int PRIMARY KEY,WarehouseName nvarchar(100),Status varchar(20),WarehouseType varchar(20),RentalEndDate date NULL);
        CREATE TABLE dbo.StorageLocation(LocationID int PRIMARY KEY,WarehouseID int,ParentLocationID int NULL,LocationName nvarchar(100),
            LocationType varchar(20),StorageType varchar(20) NULL,CapacityMode varchar(20) NULL,ConditionPolicy varchar(8) NULL,
            ZoneType varchar(20) NULL,Status varchar(20),LengthCm decimal(10,2) NULL,WidthCm decimal(10,2) NULL,
            HeightCm decimal(10,2) NULL,MaxWeightKg decimal(10,2) NULL);
        CREATE TABLE dbo.ZoneStorageRule(ZoneLocationID int,CategoryID int,BrandID int NULL);
        INSERT dbo.Warehouse VALUES(1,N'Kho A','ACTIVE','OWNED',NULL),(2,N'Kho B','ACTIVE','OWNED',NULL);
        DECLARE @warehouse int=1;
        WHILE @warehouse<=2 BEGIN
            DECLARE @offset int=(@warehouse-1)*1000;
            INSERT dbo.StorageLocation(LocationID,WarehouseID,ParentLocationID,LocationName,LocationType,StorageType,CapacityMode,ConditionPolicy,ZoneType,Status,LengthCm,WidthCm,HeightCm,MaxWeightKg) VALUES
            (@offset+1,@warehouse,NULL,'A','ZONE',NULL,NULL,NULL,'RESERVE','ACTIVE',NULL,NULL,NULL,NULL),
            (@offset+2,@warehouse,@offset+1,N'Dãy 01','ROW','FLOOR',NULL,NULL,NULL,'ACTIVE',NULL,NULL,NULL,NULL),
            (@offset+3,@warehouse,@offset+2,N'Ô 01','FLOOR_SLOT',NULL,'FLOOR_AREA','GOOD',NULL,'ACTIVE',200,120,220,500),
            (@offset+4,@warehouse,@offset+2,N'Ô cách ly','FLOOR_SLOT',NULL,'FLOOR_AREA','DAMAGED',NULL,'ACTIVE',200,120,220,500),
            (@offset+5,@warehouse,@offset+2,N'Ô gần đầy','FLOOR_SLOT',NULL,'FLOOR_AREA','GOOD',NULL,'ACTIVE',100,100,220,500),
            (@offset+6,@warehouse,@offset+2,N'Ô nhiều serial','FLOOR_SLOT',NULL,'FLOOR_AREA','GOOD',NULL,'ACTIVE',200,120,220,500),
            (@offset+7,@warehouse,@offset+2,N'Ô đồng thời','FLOOR_SLOT',NULL,'FLOOR_AREA','GOOD',NULL,'ACTIVE',200,120,220,500),
            (@offset+8,@warehouse,@offset+2,N'Ô hình học','FLOOR_SLOT',NULL,'FLOOR_AREA','GOOD',NULL,'ACTIVE',200,120,220,500),
            (@offset+9,@warehouse,@offset+2,N'Ô kiểm tra','FLOOR_SLOT',NULL,'FLOOR_AREA','GOOD',NULL,'ACTIVE',200,120,220,500),
            (@offset+10,@warehouse,NULL,'B','ZONE',NULL,NULL,NULL,'STANDARD','ACTIVE',NULL,NULL,NULL,NULL),
            (@offset+11,@warehouse,@offset+10,N'Dãy 01','ROW','FLOOR',NULL,NULL,NULL,'ACTIVE',NULL,NULL,NULL,NULL),
            (@offset+12,@warehouse,@offset+11,N'Ô 01','FLOOR_SLOT',NULL,'FLOOR_AREA','GOOD',NULL,'ACTIVE',200,120,220,500),
            (@offset+20,@warehouse,NULL,'C','ZONE',NULL,NULL,NULL,'STANDARD','ACTIVE',NULL,NULL,NULL,NULL),
            (@offset+21,@warehouse,@offset+20,N'Dãy 01','ROW','RACK',NULL,NULL,NULL,'ACTIVE',NULL,NULL,NULL,NULL),
            (@offset+22,@warehouse,@offset+21,N'Kệ 01','RACK',NULL,NULL,'GOOD',NULL,'ACTIVE',NULL,NULL,NULL,NULL),
            (@offset+23,@warehouse,@offset+22,N'Tầng 01','TIER',NULL,'VOLUME',NULL,NULL,'ACTIVE',270,110,150,1000),
            (@offset+30,@warehouse,NULL,'D','ZONE',NULL,NULL,NULL,'STANDARD','ACTIVE',NULL,NULL,NULL,NULL),
            (@offset+31,@warehouse,@offset+30,N'Dãy 01','ROW','RACK',NULL,NULL,NULL,'ACTIVE',NULL,NULL,NULL,NULL),
            (@offset+32,@warehouse,@offset+31,N'Kệ 01','RACK',NULL,NULL,'GOOD',NULL,'ACTIVE',NULL,NULL,NULL,NULL),
            (@offset+33,@warehouse,@offset+32,N'Tầng 01','TIER',NULL,'VOLUME',NULL,NULL,'ACTIVE',270,110,150,1000);
            INSERT dbo.ZoneStorageRule VALUES(@offset+1,99,999),(@offset+10,10,2),(@offset+10,11,9),(@offset+20,9,8),(@offset+20,9,9),(@offset+30,12,10),(@offset+30,13,11);
            SET @warehouse+=1;
        END;
        """;
}
