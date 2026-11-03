using Microsoft.EntityFrameworkCore;
using Warehouse.Models.Entities;
using WarehouseEntity = Warehouse.Models.Entities.Warehouse;

namespace Warehouse.DAL;

public sealed class WarehouseDbContext : DbContext
{
    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public WarehouseDbContext(DbContextOptions<WarehouseDbContext> options) : base(options) { }
    public WarehouseDbContext(string connectionString) : this(CreateOptions(connectionString)) { }

    public static DbContextOptions<WarehouseDbContext> CreateOptions(string connectionString) =>
        new DbContextOptionsBuilder<WarehouseDbContext>().UseSqlServer(connectionString).Options;

    public DbSet<WarehouseEntity> Warehouses => Set<WarehouseEntity>();
    public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();
    public DbSet<ZoneStorageRule> ZoneStorageRules => Set<ZoneStorageRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WarehouseEntity>(entity =>
        {
            entity.ToTable("Warehouse", "dbo");
            entity.HasKey(x => x.WarehouseId);
            entity.Property(x => x.WarehouseId).HasColumnName("WarehouseID");
            entity.Property(x => x.WarehouseName).HasMaxLength(150);
            entity.Property(x => x.WarehouseType).HasMaxLength(32).IsUnicode(false);
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.Property(x => x.RentalStartDate).HasColumnType("date");
            entity.Property(x => x.RentalEndDate).HasColumnType("date");
            entity.Property(x => x.Status).HasMaxLength(32).IsUnicode(false);
            entity.Property(x => x.RowVersion).IsRowVersion();
        });
        modelBuilder.Entity<StorageLocation>(entity =>
        {
            entity.ToTable("StorageLocation", "dbo");
            entity.HasKey(x => x.LocationId);
            entity.Property(x => x.LocationId).HasColumnName("LocationID");
            entity.Property(x => x.WarehouseId).HasColumnName("WarehouseID");
            entity.Property(x => x.ParentLocationId).HasColumnName("ParentLocationID");
            entity.Property(x => x.LocationName).HasMaxLength(100);
            entity.Property(x => x.LocationType).HasMaxLength(32).IsUnicode(false);
            entity.Property(x => x.ZoneType).HasMaxLength(16).IsUnicode(false);
            entity.Property(x => x.StorageType).HasMaxLength(8).IsUnicode(false);
            entity.Property(x => x.ConditionPolicy).HasMaxLength(8).IsUnicode(false);
            entity.Property(x => x.CapacityMode).HasMaxLength(16).IsUnicode(false);
            entity.Property(x => x.LengthCm).HasPrecision(10, 2);
            entity.Property(x => x.WidthCm).HasPrecision(10, 2);
            entity.Property(x => x.HeightCm).HasPrecision(10, 2);
            entity.Property(x => x.MaxWeightKg).HasPrecision(12, 3);
            entity.Property(x => x.Status).HasMaxLength(32).IsUnicode(false);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasOne(x => x.Warehouse).WithMany(x => x.Locations).HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentLocationId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<ZoneStorageRule>(entity =>
        {
            entity.ToTable("ZoneStorageRule", "dbo");
            entity.HasKey(x => x.ZoneRuleId);
            entity.Property(x => x.ZoneRuleId).HasColumnName("ZoneRuleID");
            entity.Property(x => x.ZoneLocationId).HasColumnName("ZoneLocationID");
            entity.Property(x => x.CategoryId).HasColumnName("CategoryID");
            entity.Property(x => x.BrandId).HasColumnName("BrandID");
            entity.HasOne(x => x.ZoneLocation).WithMany(x => x.ZoneRules).HasForeignKey(x => x.ZoneLocationId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
