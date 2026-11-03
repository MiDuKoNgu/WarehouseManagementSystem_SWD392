using Microsoft.EntityFrameworkCore;

namespace Catalog.DAL;

// Used by command-line tools and existing repository callers; API uses AddDbContextFactory.
internal sealed class CatalogDbContextFactory(string connectionString) : IDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext() => CatalogDbContext.Create(connectionString);
}
