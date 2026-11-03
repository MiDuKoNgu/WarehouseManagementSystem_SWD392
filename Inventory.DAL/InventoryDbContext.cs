using Inventory.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.DAL;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<Inventory.Models.Entities.Inbound> Inbounds => Set<Inventory.Models.Entities.Inbound>();
    public DbSet<InboundItem> InboundItems => Set<InboundItem>();
    public DbSet<SerialItem> SerialItems => Set<SerialItem>();
    public DbSet<SupplySource> SupplySources => Set<SupplySource>();
    public DbSet<SerialMovement> SerialMovements => Set<SerialMovement>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Inventory.Models.Entities.Inbound>(e =>
        {
            e.ToTable("Inbound", "dbo");
            e.HasKey(x => x.InboundId);
            e.Property(x => x.InboundId).HasColumnName("InboundID").HasColumnType("bigint");
            e.Property(x => x.PurchaseOrderId).HasColumnName("PurchaseOrderID").HasColumnType("bigint");
            e.Property(x => x.CreatedByUserId).HasColumnName("CreatedByUserID").HasColumnType("int");
            e.Property(x => x.PerformedByUserId).HasColumnName("PerformedByUserID").HasColumnType("int");
            e.Property(x => x.ReceiptCode).HasColumnName("ReceiptCode").HasColumnType("varchar(40)");
            e.Property(x => x.ReceiptDate).HasColumnName("ReceiptDate").HasColumnType("date");
            e.Property(x => x.Status).HasColumnName("Status").HasColumnType("varchar(32)");
            e.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2(3)");
            e.Property(x => x.CompletedAt).HasColumnName("CompletedAt").HasColumnType("datetime2(3)");
            e.Property(x => x.Note).HasColumnName("Note").HasColumnType("nvarchar(1000)");
            e.Property(x => x.RowVersion).HasColumnName("RowVersion").IsRowVersion();
        });
        model.Entity<InboundItem>(e =>
        {
            e.ToTable("InboundItem", "dbo");
            e.HasKey(x => x.InboundItemId);
            e.Property(x => x.InboundItemId).HasColumnName("InboundItemID").HasColumnType("bigint");
            e.Property(x => x.InboundId).HasColumnName("InboundID").HasColumnType("bigint");
            e.Property(x => x.POLineId).HasColumnName("POLineID").HasColumnType("bigint");
            e.Property(x => x.ScannedSerial).HasColumnName("ScannedSerial").HasColumnType("nvarchar(100)");
            e.Property(x => x.SerialItemId).HasColumnName("SerialItemID").HasColumnType("bigint");
            e.Property(x => x.ManufacturingMonth).HasColumnName("ManufacturingMonth").HasColumnType("tinyint");
            e.Property(x => x.ManufacturingYear).HasColumnName("ManufacturingYear").HasColumnType("smallint");
            e.Property(x => x.ReceivedCondition).HasColumnName("ReceivedCondition").HasColumnType("varchar(8)");
            e.Property(x => x.ConditionNote).HasColumnName("ConditionNote").HasColumnType("nvarchar(1000)");
            e.Property(x => x.ScannedAt).HasColumnName("ScannedAt").HasColumnType("datetime2(3)");
            e.Property(x => x.IsActive).HasColumnName("IsActive").HasColumnType("bit");
        });
        model.Entity<SerialItem>(e =>
        {
            e.ToTable("SerialItem", "dbo");
            e.HasKey(x => x.SerialItemId);
            e.Property(x => x.SerialItemId).HasColumnName("SerialItemID").HasColumnType("bigint");
            e.Property(x => x.ProductId).HasColumnName("ProductID").HasColumnType("int");
            e.Property(x => x.SerialNumber).HasColumnName("SerialNumber").HasColumnType("nvarchar(100)");
            e.Property(x => x.ManufacturingMonth).HasColumnName("ManufacturingMonth").HasColumnType("tinyint");
            e.Property(x => x.ManufacturingYear).HasColumnName("ManufacturingYear").HasColumnType("smallint");
            e.Property(x => x.OriginType).HasColumnName("OriginType").HasColumnType("varchar(24)");
            e.Property(x => x.OriginalInboundId).HasColumnName("OriginalInboundID").HasColumnType("bigint");
            e.Property(x => x.OriginAdjustmentId).HasColumnName("OriginAdjustmentID").HasColumnType("bigint");
            e.Property(x => x.CurrentWarehouseId).HasColumnName("CurrentWarehouseID").HasColumnType("int");
            e.Property(x => x.CurrentLocationId).HasColumnName("CurrentLocationID").HasColumnType("int");
            e.Property(x => x.CurrentSupplySourceId).HasColumnName("CurrentSupplySourceID").HasColumnType("bigint");
            e.Property(x => x.StockState).HasColumnName("StockState").HasColumnType("varchar(32)");
            e.Property(x => x.ItemCondition).HasColumnName("ItemCondition").HasColumnType("varchar(8)");
            e.Property(x => x.IsQuarantined).HasColumnName("IsQuarantined").HasColumnType("bit");
            e.Property(x => x.QuarantineReason).HasColumnName("QuarantineReason").HasColumnType("nvarchar(1000)");
            e.Property(x => x.RegisteredAt).HasColumnName("RegisteredAt").HasColumnType("datetime2(3)");
            e.Property(x => x.RowVersion).HasColumnName("RowVersion").IsRowVersion();
        });
        model.Entity<SupplySource>(e =>
        {
            e.ToTable("SupplySource", "dbo");
            e.HasKey(x => x.SupplySourceId);
            e.Property(x => x.SupplySourceId).HasColumnName("SupplySourceID").HasColumnType("bigint");
            e.Property(x => x.ProductId).HasColumnName("ProductID").HasColumnType("int");
            e.Property(x => x.WarehouseId).HasColumnName("WarehouseID").HasColumnType("int");
            e.Property(x => x.ItemCondition).HasColumnName("ItemCondition").HasColumnType("varchar(8)");
            e.Property(x => x.SourceType).HasColumnName("SourceType").HasColumnType("varchar(24)");
            e.Property(x => x.SupplyState).HasColumnName("SupplyState").HasColumnType("varchar(8)");
            e.Property(x => x.POLineId).HasColumnName("POLineID").HasColumnType("bigint");
            e.Property(x => x.InboundId).HasColumnName("InboundID").HasColumnType("bigint");
            e.Property(x => x.ReturnReceiptId).HasColumnName("ReturnReceiptID").HasColumnType("bigint");
            e.Property(x => x.TransferReceiptId).HasColumnName("TransferReceiptID").HasColumnType("bigint");
            e.Property(x => x.AdjustmentId).HasColumnName("AdjustmentID").HasColumnType("bigint");
            e.Property(x => x.EligibleQuantity).HasColumnName("EligibleQuantity").HasColumnType("int");
            e.Property(x => x.RemainingQuantity).HasColumnName("RemainingQuantity").HasColumnType("int");
            e.Property(x => x.AvailableFrom).HasColumnName("AvailableFrom").HasColumnType("date");
            e.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2(3)");
            e.Property(x => x.RowVersion).HasColumnName("RowVersion").IsRowVersion();
        });
        model.Entity<SerialMovement>(e =>
        {
            e.ToTable("SerialMovement", "dbo");
            e.HasKey(x => x.MovementId);
            e.Property(x => x.MovementId).HasColumnName("MovementID").HasColumnType("bigint");
            e.Property(x => x.SerialItemId).HasColumnName("SerialItemID").HasColumnType("bigint");
            e.Property(x => x.PerformedByUserId).HasColumnName("PerformedByUserID").HasColumnType("int");
            e.Property(x => x.EventType).HasColumnName("EventType").HasColumnType("varchar(32)");
            e.Property(x => x.OccurredAt).HasColumnName("OccurredAt").HasColumnType("datetime2(3)");
            e.Property(x => x.FromState).HasColumnName("FromState").HasColumnType("varchar(16)");
            e.Property(x => x.ToState).HasColumnName("ToState").HasColumnType("varchar(16)");
            e.Property(x => x.FromCondition).HasColumnName("FromCondition").HasColumnType("varchar(8)");
            e.Property(x => x.ToCondition).HasColumnName("ToCondition").HasColumnType("varchar(8)");
            e.Property(x => x.FromWarehouseId).HasColumnName("FromWarehouseID").HasColumnType("int");
            e.Property(x => x.ToWarehouseId).HasColumnName("ToWarehouseID").HasColumnType("int");
            e.Property(x => x.FromLocationId).HasColumnName("FromLocationID").HasColumnType("int");
            e.Property(x => x.ToLocationId).HasColumnName("ToLocationID").HasColumnType("int");
            e.Property(x => x.FromQuarantined).HasColumnName("FromQuarantined").HasColumnType("bit");
            e.Property(x => x.ToQuarantined).HasColumnName("ToQuarantined").HasColumnType("bit");
            e.Property(x => x.InboundId).HasColumnName("InboundID").HasColumnType("bigint");
            e.Property(x => x.OutboundId).HasColumnName("OutboundID").HasColumnType("bigint");
            e.Property(x => x.ReturnReceiptId).HasColumnName("ReturnReceiptID").HasColumnType("bigint");
            e.Property(x => x.DispatchId).HasColumnName("DispatchID").HasColumnType("bigint");
            e.Property(x => x.TransferReceiptId).HasColumnName("TransferReceiptID").HasColumnType("bigint");
            e.Property(x => x.AdjustmentId).HasColumnName("AdjustmentID").HasColumnType("bigint");
            e.Property(x => x.LossRequestId).HasColumnName("LossRequestID").HasColumnType("bigint");
            e.Property(x => x.Note).HasColumnName("Note").HasColumnType("nvarchar(1000)");
        });
        model.Entity<Inventory.Models.Entities.Inbound>(e =>
        {
            e.Property(x => x.WarehouseId).HasColumnName("WarehouseID");
            e.Property(x => x.CreatedByName).HasMaxLength(200);
            e.Property(x => x.PerformedByName).HasMaxLength(200);
            e.HasIndex(x => x.CreationToken).IsUnique().HasFilter("[CreationToken] IS NOT NULL");
        });
        model.Entity<InboundItem>(e =>
        {
            e.Property(x => x.InputMethod).HasColumnType("varchar(16)");
            e.Property(x => x.ScannedByUserId).HasColumnName("ScannedByUserID");
            e.Property(x => x.ScannedByName).HasMaxLength(200);
            e.HasOne(x => x.Inbound).WithMany(x => x.Items).HasForeignKey(x => x.InboundId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.SerialItem).WithMany().HasForeignKey(x => x.SerialItemId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => x.ScannedSerial).IsUnique().HasFilter("[IsActive] = 1");
            e.HasIndex(x => x.SerialItemId).IsUnique().HasFilter("[SerialItemID] IS NOT NULL");
        });
        model.Entity<SerialItem>().HasIndex(x => x.SerialNumber).IsUnique();
        model.Entity<SerialItem>().HasOne<Inventory.Models.Entities.Inbound>().WithMany().HasForeignKey(x => x.OriginalInboundId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<SerialItem>().HasOne<SupplySource>().WithMany().HasForeignKey(x => x.CurrentSupplySourceId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<SupplySource>().HasOne<Inventory.Models.Entities.Inbound>().WithMany().HasForeignKey(x => x.InboundId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<SerialMovement>().HasOne<SerialItem>().WithMany().HasForeignKey(x => x.SerialItemId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<SerialMovement>().HasOne<Inventory.Models.Entities.Inbound>().WithMany().HasForeignKey(x => x.InboundId).OnDelete(DeleteBehavior.NoAction);
    }
}
