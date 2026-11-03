using Catalog.Models.Products;
using Microsoft.EntityFrameworkCore;

namespace Catalog.DAL.Repositories;

public sealed class ProductStorageProfileRepository(IDbContextFactory<CatalogDbContext> contexts)
{
    public ProductStorageProfileRepository(string connectionString) : this(new CatalogDbContextFactory(connectionString)) { }
    public async Task<ProductStorageProfile?> FindAsync(int productId, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var p = await db.Products.AsNoTracking().Include(x => x.Category).Include(x => x.Brand)
            .SingleOrDefaultAsync(x => x.ProductId == productId, ct);
        return p is null ? null : new(p.ProductId, p.ProductCode, p.ProductName, p.CategoryId, p.Category.CategoryName,
            p.BrandId, p.Brand.BrandName, p.Status, p.Brand.Status, p.LengthCm, p.WidthCm, p.HeightCm,
            p.GrossWeightKg, Convert.ToBase64String(p.RowVersion));
    }
}
