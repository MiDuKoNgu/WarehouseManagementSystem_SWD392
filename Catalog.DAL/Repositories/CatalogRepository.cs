using Catalog.Models.Lookups;
using Microsoft.EntityFrameworkCore;

namespace Catalog.DAL.Repositories;

public sealed class CatalogRepository(IDbContextFactory<CatalogDbContext> contexts)
{
    public CatalogRepository(string connectionString) : this(new CatalogDbContextFactory(connectionString)) { }
    public async Task<CatalogOptions> GetAsync(CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var suppliers = await db.Suppliers.AsNoTracking().Where(x => x.Status == "ACTIVE").OrderBy(x => x.SupplierName)
            .Select(x => new SupplierOption(x.SupplierId, x.SupplierName)).ToListAsync(cancellationToken);
        var products = await db.Products.AsNoTracking().Where(x => x.Status == "ACTIVE" && x.Brand.Status == "ACTIVE")
            .OrderBy(x => x.ProductCode).Select(x => new ProductOption(x.ProductId, x.ProductCode, x.ProductName, x.Unit,
                x.SupplierProducts.Where(s => s.Supplier.Status == "ACTIVE").Select(s => s.SupplierId).ToList()))
            .ToListAsync(cancellationToken);
        return new(suppliers, products);
    }
}
