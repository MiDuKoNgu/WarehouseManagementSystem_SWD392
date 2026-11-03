using Catalog.BLL.Products;
using Catalog.DAL;
using Catalog.DAL.Repositories;
using Catalog.Models.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Products.Tests;

internal static class SqlProductTests
{
    public static void CheckModel()
    {
        using var db = CatalogDbContext.Create("Server=unused;Database=CatalogModelOnly;Integrated Security=true;TrustServerCertificate=true");
        var product = db.Model.FindEntityType(typeof(Product))!;
        ProductTests.Check(product.GetTableName() == "Product" && product.GetSchema() == "dbo", "product table mapping");
        ProductTests.Check(product.FindProperty(nameof(Product.RowVersion))!.IsConcurrencyToken, "product rowversion concurrency");
        ProductTests.Check(product.FindProperty(nameof(Product.GrossWeightKg))!.GetScale() == 3, "weight precision mapping");
        ProductTests.Check(db.Model.FindEntityType(typeof(SupplierProduct))!.FindPrimaryKey()!.Properties.Count == 2, "supplier-product composite key");
        // SQL generation catches unsupported LINQ before a database is needed.
        var sql = db.Products.Where(p => p.SupplierProducts.Any(s => s.SupplierId == 1))
            .Select(p => new { p.ProductId, p.Category.CategoryName, p.Brand.BrandName }).ToQueryString();
        ProductTests.Check(sql.Contains("EXISTS") && sql.Contains("JOIN"), "entity relationship query translates");
    }

    public static async Task Run(string server)
    {
        // Only a unique database created by this test is deleted below.
        var database = "EWMS_EfCatalogTest_" + Guid.NewGuid().ToString("N");
        var connection = new SqlConnectionStringBuilder
        { DataSource = server, InitialCatalog = database, IntegratedSecurity = true, TrustServerCertificate = true }.ConnectionString;
        await using var db = CatalogDbContext.Create(connection);
        try
        {
            await db.Database.EnsureCreatedAsync();
            var category = new Category { CategoryName = "Test category" };
            var brand = new Brand { BrandName = "Test brand" };
            var supplier = new Supplier { SupplierName = "Test supplier" };
            db.AddRange(category, brand, supplier);
            await db.SaveChangesAsync();
            var repo = new SqlProductRepository(connection);
            var input = ProductTests.Valid();
            ProductService.Validate(input);
            input.CategoryId = category.CategoryId; input.BrandId = brand.BrandId; input.SupplierIds = [supplier.SupplierId];
            var saved = await repo.SaveAsync(null, input, 22, null, default);
            ProductTests.Check(saved.Suppliers.Length == 1 && Convert.FromBase64String(saved.RowVersion).Length == 8, "entity insert and generated version");
            ProductTests.Check(saved.CreatedAt.Kind == DateTimeKind.Utc && saved.CreatedByUserId == 22, "server creation metadata");
            var oldVersion = Convert.FromBase64String(saved.RowVersion);
            input.ProductName = "Updated product";
            var updated = await repo.SaveAsync(saved.ProductId, input, 99, oldVersion, default);
            ProductTests.Check(updated.ProductName == input.ProductName && updated.RowVersion != saved.RowVersion
                && updated.CreatedByUserId == 22, "tracked update preserves creator and advances version");
            await Reject<ProductConflictException>(() => repo.SaveAsync(saved.ProductId, input, 22, oldVersion, default), "stale version rejected");
            await Reject<ProductConflictException>(() => repo.SaveAsync(null, input, 22, null, default), "duplicate code rejected");
            input.SupplierIds = [int.MaxValue];
            await Reject<ProductValidationException>(() => repo.SaveAsync(saved.ProductId, input, 22, Convert.FromBase64String(updated.RowVersion), default), "invalid supplier rejected");
            var unchanged = await repo.FindAsync(saved.ProductId, default);
            ProductTests.Check(unchanged!.RowVersion == updated.RowVersion && unchanged.Suppliers.Length == 1, "failed save rolls back all changes");
            var page = await repo.ListAsync(new() { SupplierId = supplier.SupplierId, Search = "TEST-AC" }, default);
            ProductTests.Check(page.TotalCount == 1 && page.Items.Length == 1, "LINQ filters and count");
            var literal = await repo.ListAsync(new() { Search = "%" }, default);
            ProductTests.Check(literal.TotalCount == 0, "search escapes SQL wildcards");
            var lookup = await new CatalogLookupRepository(connection).GetAsync(default);
            ProductTests.Check(lookup.Products.Single().SupplierIds.SequenceEqual(new[] { supplier.SupplierId }), "lookup supplier projection");
            var profile = await new ProductStorageProfileRepository(connection).FindAsync(saved.ProductId, default);
            ProductTests.Check(profile!.RowVersion == updated.RowVersion && profile.BrandName == brand.BrandName, "storage profile projection");
            Console.WriteLine("PASS Catalog EF SQL insert/update/rollback/concurrency/queries.");
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static async Task Reject<T>(Func<Task> action, string label) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new Exception("FAIL: " + label);
    }
}
