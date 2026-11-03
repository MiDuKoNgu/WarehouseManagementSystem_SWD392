using Microsoft.EntityFrameworkCore;
using Procurement.Models.Entities;

namespace Procurement.DAL;

// Compatibility for command-line callers and integration tests, with a fresh unit of work per operation.
internal sealed class ProcurementContextFactory(string connectionString) : IDbContextFactory<ProcurementDbContext>
{
    private readonly DbContextOptions<ProcurementDbContext> options = new DbContextOptionsBuilder<ProcurementDbContext>()
        .UseSqlServer(connectionString).Options;
    public ProcurementDbContext CreateDbContext() => new(options);
}
