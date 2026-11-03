using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Procurement.DAL;
using Procurement.Models.Entities;

namespace Procurement.Tests;

internal static class EfMappingTests
{
    public static void Run()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseSqlServer("Server=localhost;Database=MappingOnly;Integrated Security=true;TrustServerCertificate=true").Options);
        var order = db.Model.FindEntityType(typeof(PurchaseOrder))!;
        var table = StoreObjectIdentifier.Table("PurchaseOrder", "dbo");
        ProcurementTests.Check(order.GetTableName() == "PurchaseOrder" && order.GetSchema() == "dbo", "EF maps existing PO table");
        ProcurementTests.Check(order.FindProperty(nameof(PurchaseOrder.PoCode))!.GetColumnName(table) == "POCode", "EF maps legacy PO column names");
        var version = order.FindProperty(nameof(PurchaseOrder.RowVersion))!;
        ProcurementTests.Check(version.IsConcurrencyToken && version.ValueGenerated == ValueGenerated.OnAddOrUpdate, "EF treats rowversion as database-generated concurrency token");
        var line = db.Model.FindEntityType(typeof(PurchaseOrderLine))!;
        ProcurementTests.Check(line.GetForeignKeys().Single().PrincipalEntityType == order, "PO line entity relationship stays inside Procurement");
        ProcurementTests.Check(db.Model.FindEntityType(typeof(ReceivingReservation))!.FindProperty(nameof(ReceivingReservation.Token))!.ValueGenerated == ValueGenerated.Never,
            "reservation idempotency token is supplied by caller");
        var sql = db.PurchaseOrders.AsNoTracking().Include(x => x.Lines).Where(x => x.PurchaseOrderId == 1).ToQueryString();
        ProcurementTests.Check(sql.Contains("[PurchaseOrderLine]") && sql.Contains("[POLineID]"), "EF translates PO and line reads against existing schema");
        var lockedSql = db.PurchaseOrders.FromSqlInterpolated($"SELECT * FROM dbo.PurchaseOrder WITH (UPDLOCK,HOLDLOCK) WHERE PurchaseOrderID = {1L}").ToQueryString();
        ProcurementTests.Check(lockedSql.Contains("UPDLOCK,HOLDLOCK"), "EF preserves shared reservation/transition row-lock protocol");
    }
}
