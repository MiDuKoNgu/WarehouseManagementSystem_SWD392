using Microsoft.EntityFrameworkCore;
using Procurement.Models.Entities;

namespace Procurement.DAL;

/// <summary>Maps the existing Procurement database; schema changes are never run on startup.</summary>
public sealed class ProcurementDbContext(DbContextOptions<ProcurementDbContext> options) : DbContext(options)
{
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<ReceivingReservation> ReceivingReservations => Set<ReceivingReservation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var order = modelBuilder.Entity<PurchaseOrder>();
        order.ToTable("PurchaseOrder", "dbo");
        order.HasKey(x => x.PurchaseOrderId);
        order.Property(x => x.PurchaseOrderId).HasColumnName("PurchaseOrderID");
        order.Property(x => x.PoCode).HasColumnName("POCode").HasMaxLength(40).IsUnicode(false);
        order.Property(x => x.SupplierId).HasColumnName("SupplierID");
        order.Property(x => x.WarehouseId).HasColumnName("WarehouseID");
        order.Property(x => x.CreatedByUserId).HasColumnName("CreatedByUserID");
        order.Property(x => x.OrderDate).HasColumnType("date");
        order.Property(x => x.ExpectedArrivalDate).HasColumnType("date");
        order.Property(x => x.Status).HasMaxLength(32).IsUnicode(false);
        order.Property(x => x.Note).HasMaxLength(1000);
        order.Property(x => x.CloseReason).HasMaxLength(1000);
        order.Property(x => x.CreatedAt).HasPrecision(3);
        order.Property(x => x.ConfirmedAt).HasPrecision(3);
        order.Property(x => x.ClosedAt).HasPrecision(3);
        order.Property(x => x.RowVersion).IsRowVersion();
        order.HasIndex(x => x.PoCode).IsUnique();

        var line = modelBuilder.Entity<PurchaseOrderLine>();
        line.ToTable("PurchaseOrderLine", "dbo");
        line.HasKey(x => x.PoLineId);
        line.Property(x => x.PoLineId).HasColumnName("POLineID");
        line.Property(x => x.PurchaseOrderId).HasColumnName("PurchaseOrderID");
        line.Property(x => x.ProductId).HasColumnName("ProductID");
        line.HasOne(x => x.PurchaseOrder).WithMany(x => x.Lines).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.NoAction);

        var reservation = modelBuilder.Entity<ReceivingReservation>();
        reservation.ToTable("ReceivingReservation", "dbo");
        reservation.HasKey(x => x.Token);
        reservation.Property(x => x.Token).ValueGeneratedNever();
        reservation.Property(x => x.PurchaseOrderId).HasColumnName("PurchaseOrderID");
        reservation.Property(x => x.WarehouseId).HasColumnName("WarehouseID");
        reservation.Property(x => x.CreatedByUserId).HasColumnName("CreatedByUserID");
        reservation.Property(x => x.Status).HasMaxLength(16).IsUnicode(false);
        reservation.Property(x => x.CreatedAt).HasPrecision(3);
        reservation.Property(x => x.ReleasedAt).HasPrecision(3);
        reservation.HasOne(x => x.PurchaseOrder).WithMany(x => x.ReceivingReservations).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.NoAction);

        var audit = modelBuilder.Entity<AuditLog>();
        audit.ToTable("AuditLog", "dbo");
        audit.HasKey(x => x.AuditLogId);
        audit.Property(x => x.AuditLogId).HasColumnName("AuditLogID");
        audit.Property(x => x.UserId).HasColumnName("UserID");
        audit.Property(x => x.Action).HasMaxLength(60).IsUnicode(false);
        audit.Property(x => x.EntityName).HasMaxLength(100).IsUnicode(false);
        audit.Property(x => x.EntityId).HasColumnName("EntityID").HasMaxLength(100);
        audit.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
    }
}

// Compatibility for command-line callers and integration tests, with a fresh unit of work per operation.
internal sealed class ProcurementContextFactory(string connectionString) : IDbContextFactory<ProcurementDbContext>
{
    private readonly DbContextOptions<ProcurementDbContext> options = new DbContextOptionsBuilder<ProcurementDbContext>()
        .UseSqlServer(connectionString).Options;
    public ProcurementDbContext CreateDbContext() => new(options);
}
