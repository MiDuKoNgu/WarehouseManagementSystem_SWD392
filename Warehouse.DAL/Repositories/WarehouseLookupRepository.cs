using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Warehouse.Models.Lookups;

namespace Warehouse.DAL.Repositories;

public sealed class WarehouseLookupRepository(IDbContextFactory<WarehouseDbContext> contexts)
{
    public WarehouseLookupRepository(string connectionString)
        : this(new PooledDbContextFactory<WarehouseDbContext>(WarehouseDbContext.CreateOptions(connectionString))) { }

    public async Task<List<WarehouseOption>> GetAsync(CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.Warehouses.AsNoTracking().Where(x => x.Status == "ACTIVE")
            .OrderBy(x => x.WarehouseName)
            .Select(x => new WarehouseOption(x.WarehouseId, x.WarehouseName, x.Address))
            .ToListAsync(cancellationToken);
    }
}
