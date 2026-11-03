using System.Data;
using Catalog.BLL.Products;
using Catalog.Models.Entities;
using Catalog.Models.Products;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Catalog.DAL.Repositories;

public sealed class SqlProductRepository(IDbContextFactory<CatalogDbContext> contexts) : IProductRepository
{
    public SqlProductRepository(string connectionString) : this(new CatalogDbContextFactory(connectionString)) { }

    public async Task<ProductPage> ListAsync(ProductQuery query, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var products = db.Products.AsNoTracking().AsQueryable();
        if (query.Search is { } search)
        {
            var pattern = "%" + search.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[") + "%";
            products = products.Where(p => EF.Functions.Like(p.ProductCode, pattern, "~")
                || EF.Functions.Like(p.ProductName, pattern, "~") || EF.Functions.Like(p.Model, pattern, "~"));
        }
        if (query.CategoryId is { } category) products = products.Where(p => p.CategoryId == category);
        if (query.BrandId is { } brand) products = products.Where(p => p.BrandId == brand);
        if (query.Status is { } status) products = products.Where(p => p.Status == status);
        if (query.SupplierId is { } supplier) products = products.Where(p => p.SupplierProducts.Any(s => s.SupplierId == supplier));
        var total = await products.CountAsync(ct);
        var items = await products.OrderBy(p => p.ProductCode).ThenBy(p => p.ProductId)
            .Skip(checked((query.Page - 1) * query.PageSize)).Take(query.PageSize)
            .Select(p => new ProductSummary(p.ProductId, p.ProductCode, p.ProductName, p.Model, p.Unit,
                p.CategoryId, p.Category.CategoryName, p.BrandId, p.Brand.BrandName, p.Status)).ToArrayAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }
    public async Task<ProductDetail?> FindAsync(int id, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await ReadDetail(db, id, ct);
    }
    public async Task<ProductOptions> OptionsAsync(CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var categories = await db.Categories.AsNoTracking().OrderBy(x => x.CategoryName)
            .Select(x => new ProductCategory(x.CategoryId, x.CategoryName)).ToArrayAsync(ct);
        var brands = await db.Brands.AsNoTracking().Where(x => x.Status == "ACTIVE").OrderBy(x => x.BrandName)
            .Select(x => new ProductBrand(x.BrandId, x.BrandName)).ToArrayAsync(ct);
        var suppliers = await db.Suppliers.AsNoTracking().Where(x => x.Status == "ACTIVE").OrderBy(x => x.SupplierName)
            .Select(x => new ProductSupplier(x.SupplierId, x.SupplierName)).ToArrayAsync(ct);
        return new(categories, brands, suppliers);
    }
    public async Task<ProductDetail> SaveAsync(int? id, ProductInput input, int actorId, byte[]? rowVersion, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            Product product;
            if (id is { } productId)
            {
                // SQL Server lock hints preserve the original update-lock ordering.
                product = await db.Products.FromSqlInterpolated($"SELECT * FROM dbo.Product WITH (UPDLOCK,HOLDLOCK) WHERE ProductID={productId}")
                    .SingleOrDefaultAsync(ct) ?? throw new ProductNotFoundException();
                if (!product.ProductCode.Equals(input.ProductCode, StringComparison.OrdinalIgnoreCase))
                    throw new ProductValidationException(["Không được thay đổi mã sản phẩm sau khi tạo."]);
                if (rowVersion is null || !product.RowVersion.SequenceEqual(rowVersion))
                    throw new ProductConflictException("Sản phẩm đã được người khác sửa. Hãy tải lại trang trước khi lưu.");
                db.Entry(product).Property(x => x.RowVersion).OriginalValue = rowVersion;
                product.UpdatedAt = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct);
            }
            else
            {
                product = new Product { ProductCode = input.ProductCode, CreatedByUserId = actorId };
                db.Products.Add(product);
            }
            var category = await db.Categories.FromSqlInterpolated($"SELECT * FROM dbo.Category WITH (UPDLOCK,HOLDLOCK) WHERE CategoryID={input.CategoryId}").AnyAsync(ct);
            var brand = await db.Brands.FromSqlInterpolated($"SELECT * FROM dbo.Brand WITH (UPDLOCK,HOLDLOCK) WHERE BrandID={input.BrandId} AND Status='ACTIVE'").AnyAsync(ct);
            if (!category || !brand)
                throw new ProductValidationException(["Danh mục không tồn tại hoặc hãng không hoạt động."]);
            var supplierCount = await db.Suppliers.FromSqlRaw("SELECT * FROM dbo.Supplier WITH (UPDLOCK,HOLDLOCK)")
                .Where(x => x.Status == "ACTIVE" && input.SupplierIds.Contains(x.SupplierId)).CountAsync(ct);
            if (supplierCount != input.SupplierIds.Length)
                throw new ProductValidationException(["Một hoặc nhiều nhà cung cấp không tồn tại hoặc không hoạt động."]);

            product.CategoryId = input.CategoryId; product.BrandId = input.BrandId;
            product.ProductName = input.ProductName; product.Model = input.Model; product.Unit = input.Unit;
            product.Specifications = input.Specifications; product.LengthCm = input.LengthCm;
            product.WidthCm = input.WidthCm; product.HeightCm = input.HeightCm;
            product.GrossWeightKg = input.GrossWeightKg; product.Status = input.Status;
            await db.SaveChangesAsync(ct);
            var currentLinks = await db.SupplierProducts.Where(x => x.ProductId == product.ProductId).ToListAsync(ct);
            db.SupplierProducts.RemoveRange(currentLinks.Where(x => !input.SupplierIds.Contains(x.SupplierId)));
            foreach (var supplierId in input.SupplierIds.Except(currentLinks.Select(x => x.SupplierId)))
                db.SupplierProducts.Add(new SupplierProduct { SupplierId = supplierId, ProductId = product.ProductId });
            await db.SaveChangesAsync(ct);
            var detail = await ReadDetail(db, product.ProductId, ct) ?? throw new InvalidOperationException("Saved product is missing.");
            await transaction.CommitAsync(ct);
            return detail;
        }
        catch (DbUpdateConcurrencyException)
        { throw new ProductConflictException("Phiên bản sản phẩm đã thay đổi. Hãy tải lại trang."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new ProductConflictException("Mã sản phẩm đã tồn tại. Vui lòng chọn mã khác."); }
    }
    private static async Task<ProductDetail?> ReadDetail(CatalogDbContext db, int id, CancellationToken ct)
    {
        var p = await db.Products.AsNoTracking().Include(x => x.Category).Include(x => x.Brand)
            .SingleOrDefaultAsync(x => x.ProductId == id, ct);
        if (p is null) return null;
        var suppliers = await db.SupplierProducts.AsNoTracking().Where(x => x.ProductId == id)
            .OrderBy(x => x.Supplier.SupplierName).Select(x => new ProductSupplier(x.SupplierId, x.Supplier.SupplierName)).ToArrayAsync(ct);
        return new(p.ProductId, p.ProductCode, p.ProductName, p.Model, p.Unit, p.CategoryId, p.Category.CategoryName,
            p.BrandId, p.Brand.BrandName, p.Status, p.Specifications, p.LengthCm, p.WidthCm, p.HeightCm,
            p.GrossWeightKg, suppliers, p.CreatedByUserId, DateTime.SpecifyKind(p.CreatedAt, DateTimeKind.Utc),
            p.UpdatedAt is { } updated ? DateTime.SpecifyKind(updated, DateTimeKind.Utc) : null, Convert.ToBase64String(p.RowVersion));
    }
}
