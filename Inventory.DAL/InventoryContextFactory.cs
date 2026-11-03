using Inventory.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.DAL;

// Compatibility factory for CLI callers and integration tests; API uses AddDbContextFactory.
public sealed class InventoryContextFactory(string connectionString) : IDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext() => new(new DbContextOptionsBuilder<InventoryDbContext>()
        .UseSqlServer(connectionString).Options);
}