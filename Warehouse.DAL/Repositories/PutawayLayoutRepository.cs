using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Warehouse.Models.Lookups;

namespace Warehouse.DAL.Repositories;

public sealed class PutawayLayoutRepository(IDbContextFactory<WarehouseDbContext> contexts)
{
    public PutawayLayoutRepository(string connectionString)
        : this(new PooledDbContextFactory<WarehouseDbContext>(WarehouseDbContext.CreateOptions(connectionString))) { }

    public async Task<PutawayLayout?> GetAsync(int warehouseId, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        // A snapshot only: Inventory revalidates these rows in its committing transaction.
        var warehouse = await db.Warehouses.AsNoTracking().SingleOrDefaultAsync(x => x.WarehouseId == warehouseId, ct);
        if (warehouse is null) return null;
        var locations = await db.StorageLocations.AsNoTracking().Where(x => x.WarehouseId == warehouseId)
            .OrderBy(x => x.LocationId).ToArrayAsync(ct);
        var rules = await db.ZoneStorageRules.AsNoTracking().Where(x => x.ZoneLocation.WarehouseId == warehouseId)
            .OrderBy(x => x.ZoneLocationId).ThenBy(x => x.CategoryId).ThenBy(x => x.BrandId)
            .Select(x => new PutawayZoneRule(x.ZoneLocationId, x.CategoryId, x.BrandId)).ToArrayAsync(ct);
        return new(new(warehouse.WarehouseId, warehouse.WarehouseName, warehouse.WarehouseType,
                warehouse.RentalStartDate, warehouse.RentalEndDate, warehouse.Status, Convert.ToBase64String(warehouse.RowVersion)),
            locations.Select(x => new PutawayLocation(x.LocationId, x.WarehouseId, x.ParentLocationId, x.LocationName,
                x.LocationType, x.ZoneType, x.StorageType, x.ConditionPolicy, x.CapacityMode, x.LengthCm, x.WidthCm,
                x.HeightCm, x.MaxWeightKg, x.Status, Convert.ToBase64String(x.RowVersion))).ToArray(), rules);
    }
}
