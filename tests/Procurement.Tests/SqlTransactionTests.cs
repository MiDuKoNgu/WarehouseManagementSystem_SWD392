using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Procurement.DAL.PurchaseOrders;
using Procurement.DAL.Receiving;
using Procurement.Models.PurchaseOrders;

namespace Procurement.Tests;

internal static class SqlTransactionTests
{
    public static async Task Run(string server)
    {
        var database = "EWMS_ProcurementTests_" + Guid.NewGuid().ToString("N");
        var baseConnection = new SqlConnectionStringBuilder
        {
            DataSource = server, InitialCatalog = "master", IntegratedSecurity = true,
            TrustServerCertificate = true, ConnectTimeout = 10
        };
        await using var master = new SqlConnection(baseConnection.ConnectionString);
        await master.OpenAsync();
        await using (var create = new SqlCommand($"CREATE DATABASE [{database}]", master)) await create.ExecuteNonQueryAsync();
        try
        {
            baseConnection.InitialCatalog = database;
            await using var connection = new SqlConnection(baseConnection.ConnectionString);
            await connection.OpenAsync();
            await using (var schema = new SqlCommand("""
                CREATE TABLE dbo.PurchaseOrder (
                    PurchaseOrderID bigint IDENTITY PRIMARY KEY, SupplierID int NOT NULL, WarehouseID int NOT NULL,
                    CreatedByUserID int NOT NULL, POCode varchar(40) NOT NULL UNIQUE, OrderDate date NOT NULL,
                    ExpectedArrivalDate date NULL, Status varchar(32) NOT NULL, Note nvarchar(1000) NULL, CreatedAt datetime2 NOT NULL,
                    RowVersion rowversion NOT NULL, ConfirmedAt datetime2 NULL, ClosedAt datetime2 NULL, CloseReason nvarchar(1000) NULL);
                CREATE TABLE dbo.PurchaseOrderLine (
                    POLineID bigint IDENTITY PRIMARY KEY, PurchaseOrderID bigint NOT NULL REFERENCES dbo.PurchaseOrder(PurchaseOrderID),
                    ProductID int NOT NULL, OrderedQuantity int NOT NULL, ClosedQuantity int NOT NULL);
                CREATE TABLE dbo.ReceivingReservation (
                    Token uniqueidentifier PRIMARY KEY, PurchaseOrderID bigint NOT NULL REFERENCES dbo.PurchaseOrder(PurchaseOrderID),
                    WarehouseID int NOT NULL, CreatedByUserID int NOT NULL, Status varchar(16) NOT NULL,
                    CreatedAt datetime2 NOT NULL, ReleasedAt datetime2 NULL);
                CREATE TABLE dbo.AuditLog (
                    AuditLogID bigint IDENTITY PRIMARY KEY, UserID int NOT NULL, Action varchar(60) NOT NULL,
                    EntityName varchar(100) NOT NULL, EntityID nvarchar(100) NOT NULL, OldValues nvarchar(max) NULL,
                    NewValues nvarchar(max) NULL, Status varchar(20) NOT NULL, CreatedAt datetime2 NOT NULL,
                    CONSTRAINT RejectAudit CHECK (Action = 'REJECT_ALL_CREATION_ACTIONS'));
                """, connection)) await schema.ExecuteNonQueryAsync();
            var repo = new SqlPurchaseOrderRepository(baseConnection.ConnectionString);
            var request = new CreatePurchaseOrder(1, 1, null, "SQL integration", [new(1, 3), new(2, 5)]);
            var failed = false;
            try { await repo.CreateAsync(request, 22, "SQL Test", "CREATE_PO", CancellationToken.None); }
            catch (DbUpdateException error) when (error.InnerException is SqlException) { failed = true; }
            ProcurementTests.Check(failed, "audit write forced to fail");
            await using (var count = new SqlCommand("SELECT (SELECT COUNT(*) FROM dbo.PurchaseOrder) + (SELECT COUNT(*) FROM dbo.PurchaseOrderLine) + (SELECT COUNT(*) FROM dbo.AuditLog)", connection))
                ProcurementTests.Check(Convert.ToInt32(await count.ExecuteScalarAsync()) == 0, "audit failure rolls back all PO data");
            await using (var alter = new SqlCommand("ALTER TABLE dbo.AuditLog DROP CONSTRAINT RejectAudit", connection)) await alter.ExecuteNonQueryAsync();
            var first = await repo.CreateAsync(request, 22, "SQL Test", "IMPORT_PO_EXCEL", CancellationToken.None, new string('A', 64));
            var retry = await repo.CreateAsync(request, 22, "SQL Test", "IMPORT_PO_EXCEL", CancellationToken.None, new string('A', 64));
            ProcurementTests.Check(first.PurchaseOrderId == retry.PurchaseOrderId, "durable import key prevents duplicate SQL writes");
            var detail = await repo.FindAsync(first.PurchaseOrderId, CancellationToken.None);
            ProcurementTests.Check(detail is { Status: "DRAFT", Lines.Length: 2 }, "SQL detail reads committed draft and lines");
            await using (var count = new SqlCommand("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action = 'IMPORT_PO_EXCEL' AND UserID = 22 AND ISJSON(NewValues)=1", connection))
                ProcurementTests.Check(Convert.ToInt32(await count.ExecuteScalarAsync()) == 1, "one audit with JSON and creator");
            var editedRequest = request with { Note = "Edited PO", Lines = [new(1, 4), new(2, 5)] };
            await using (var rejectEdit = new SqlCommand("ALTER TABLE dbo.AuditLog ADD CONSTRAINT RejectEditAudit CHECK (Action <> 'UPDATE_PO')", connection)) await rejectEdit.ExecuteNonQueryAsync();
            failed = false;
            try { await repo.UpdateAsync(first, editedRequest, 23, "Editor", CancellationToken.None); }
            catch (DbUpdateException error) when (error.InnerException is SqlException) { failed = true; }
            var untouched = (await repo.FindAsync(first.PurchaseOrderId, CancellationToken.None))!;
            ProcurementTests.Check(failed && untouched.RowVersion == first.RowVersion && untouched.Lines[0].Quantity == 3, "edit audit failure rolls back header, lines and row version");
            await using (var allowEdit = new SqlCommand("ALTER TABLE dbo.AuditLog DROP CONSTRAINT RejectEditAudit", connection)) await allowEdit.ExecuteNonQueryAsync();
            var edited = await repo.UpdateAsync(untouched, editedRequest, 23, "Editor", CancellationToken.None);
            ProcurementTests.Check(edited.RowVersion != first.RowVersion && edited.Lines[0].PoLineId == first.Lines[0].PoLineId, "edit changes version while retaining matching POLine IDs");
            var staleBlocked = false;
            try { await repo.UpdateAsync(first, editedRequest, 23, "Editor", CancellationToken.None); }
            catch (Procurement.BLL.PurchaseOrders.PurchaseOrderConflictException) { staleBlocked = true; }
            ProcurementTests.Check(staleBlocked, "stale SQL edit rejected under lock");
            var transitionResults = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            {
                try { return await repo.TransitionAsync(edited, "CONFIRMED", null, new Dictionary<long,int>(), 23, "Editor", CancellationToken.None); }
                catch (Procurement.BLL.PurchaseOrders.PurchaseOrderConflictException) { return null; }
            }));
            ProcurementTests.Check(transitionResults.Count(value => value is not null) == 1, "concurrent confirmation commits once");
            var confirmed = transitionResults.Single(value => value is not null)!;
            var reservationRepository = new SqlReceivingReservationRepository(baseConnection.ConnectionString);
            var token = Guid.NewGuid();
            var wrongWarehouseBlocked = false;
            try { await reservationRepository.ReserveAsync(confirmed.PurchaseOrderId, token, 99, 24, CancellationToken.None); }
            catch (Procurement.BLL.PurchaseOrders.PurchaseOrderNotFoundException) { wrongWarehouseBlocked = true; }
            ProcurementTests.Check(wrongWarehouseBlocked, "receipt reservation respects assigned warehouse");
            await reservationRepository.ReserveAsync(confirmed.PurchaseOrderId, token, 1, 24, CancellationToken.None);
            await reservationRepository.ReserveAsync(confirmed.PurchaseOrderId, token, 1, 24, CancellationToken.None);
            var reservedOrder = (await repo.FindAsync(confirmed.PurchaseOrderId, CancellationToken.None))!;
            ProcurementTests.Check(reservedOrder.RowVersion != confirmed.RowVersion, "receipt reservation invalidates stale PO snapshots");
            var closeBlocked = false;
            try { await repo.TransitionAsync(reservedOrder, "CLOSED", "Short delivery",
                reservedOrder.Lines.ToDictionary(line => line.PoLineId, line => line.Quantity), 23, "Editor", CancellationToken.None); }
            catch (Procurement.BLL.PurchaseOrders.PurchaseOrderConflictException) { closeBlocked = true; }
            ProcurementTests.Check(closeBlocked, "open receipt reservation prevents concurrent PO close");
            await using (var reservationCount = new SqlCommand("SELECT COUNT(*) FROM dbo.ReceivingReservation", connection))
                ProcurementTests.Check(Convert.ToInt32(await reservationCount.ExecuteScalarAsync()) == 1, "reservation retry is idempotent");
            await reservationRepository.ReleaseAsync(confirmed.PurchaseOrderId, token, 1, CancellationToken.None);
            confirmed = (await repo.FindAsync(confirmed.PurchaseOrderId, CancellationToken.None))!;
            ProcurementTests.Check(confirmed.RowVersion != reservedOrder.RowVersion, "receipt release invalidates stale received quantity snapshots");
            var closed = await repo.TransitionAsync(confirmed, "CLOSED", "Supplier shortage",
                confirmed.Lines.ToDictionary(line => line.PoLineId, line => line.Quantity - (line.ProductId == 1 ? 1 : 0)), 23, "Editor", CancellationToken.None);
            ProcurementTests.Check(closed is { Status: "CLOSED", ClosedAt: not null, CloseReason: "Supplier shortage" }
                && closed.Lines[0].ClosedQuantity == 3 && closed.Lines[1].ClosedQuantity == 5, "SQL close saves remaining quantities and reason");
            var cancellable = await repo.CreateAsync(request, 22, "Creator", "CREATE_PO", CancellationToken.None);
            await using (var rejectCancel = new SqlCommand("ALTER TABLE dbo.AuditLog ADD CONSTRAINT RejectCancelAudit CHECK (Action <> 'CANCEL_PO')", connection)) await rejectCancel.ExecuteNonQueryAsync();
            failed = false;
            try { await repo.TransitionAsync(cancellable, "CANCELLED", "Not needed", new Dictionary<long,int>(), 23, "Editor", CancellationToken.None); }
            catch (DbUpdateException error) when (error.InnerException is SqlException) { failed = true; }
            var stillDraft = (await repo.FindAsync(cancellable.PurchaseOrderId, CancellationToken.None))!;
            ProcurementTests.Check(failed && stillDraft.Status == "DRAFT" && stillDraft.ClosedAt is null && stillDraft.RowVersion == cancellable.RowVersion, "cancel audit failure rolls back transition");
            await using (var allowCancel = new SqlCommand("ALTER TABLE dbo.AuditLog DROP CONSTRAINT RejectCancelAudit", connection)) await allowCancel.ExecuteNonQueryAsync();
            await repo.TransitionAsync(stillDraft, "CANCELLED", "Not needed", new Dictionary<long,int>(), 23, "Editor", CancellationToken.None);
            await using (var auditCount = new SqlCommand("SELECT COUNT(*) FROM dbo.AuditLog WHERE UserID=23 AND Action IN ('UPDATE_PO','CONFIRM_PO','CLOSE_PO','CANCEL_PO') AND ISJSON(OldValues)=1 AND JSON_VALUE(NewValues,'$.performedByName')='Editor'", connection))
                ProcurementTests.Check(Convert.ToInt32(await auditCount.ExecuteScalarAsync()) == 4, "all successful transitions have before/after actor audit once");
            Console.WriteLine("PASS SQL: audit failure rollback, committed Draft/lines/audit, durable import retry. Disposable test DB only.");
        }
        finally
        {
            // Only the generated disposable database can be removed, never a configured business DB.
            if (!database.StartsWith("EWMS_ProcurementTests_", StringComparison.Ordinal) || database.Length != 54)
                throw new InvalidOperationException("Refusing cleanup outside generated test database.");
            SqlConnection.ClearAllPools();
            await using var cleanup = new SqlCommand($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]", master);
            await cleanup.ExecuteNonQueryAsync();
        }
    }
}
