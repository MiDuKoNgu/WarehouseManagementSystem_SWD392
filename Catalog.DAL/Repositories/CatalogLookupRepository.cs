using Catalog.Models.Lookups;
using Microsoft.Data.SqlClient;

namespace Catalog.DAL.Repositories;

public sealed class CatalogLookupRepository(string connectionString)
{
    public async Task<CatalogOptions> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT SupplierID, SupplierName FROM dbo.Supplier WHERE Status = 'ACTIVE' ORDER BY SupplierName;
            SELECT p.ProductID, p.ProductCode, p.ProductName, p.Unit
            FROM dbo.Product p JOIN dbo.Brand b ON b.BrandID = p.BrandID
            JOIN dbo.Category c ON c.CategoryID = p.CategoryID
            WHERE p.Status = 'ACTIVE' AND b.Status = 'ACTIVE' ORDER BY p.ProductCode;
            SELECT sp.ProductID, sp.SupplierID FROM dbo.SupplierProduct sp
            JOIN dbo.Supplier s ON s.SupplierID = sp.SupplierID WHERE s.Status = 'ACTIVE';
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var suppliers = new List<SupplierOption>();
        while (await reader.ReadAsync(cancellationToken)) suppliers.Add(new(reader.GetInt32(0), reader.GetString(1)));
        await reader.NextResultAsync(cancellationToken);
        var products = new List<ProductOption>();
        while (await reader.ReadAsync(cancellationToken)) products.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), []));
        await reader.NextResultAsync(cancellationToken);
        var byId = products.ToDictionary(product => product.ProductId);
        while (await reader.ReadAsync(cancellationToken))
            if (byId.TryGetValue(reader.GetInt32(0), out var product)) product.SupplierIds.Add(reader.GetInt32(1));
        return new(suppliers, products);
    }
}
