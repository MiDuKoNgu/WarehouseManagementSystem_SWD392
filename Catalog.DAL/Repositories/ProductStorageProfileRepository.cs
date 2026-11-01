using System.Data;
using Catalog.Models.Products;
using Microsoft.Data.SqlClient;

namespace Catalog.DAL.Repositories;

public sealed class ProductStorageProfileRepository(string connectionString)
{
    public async Task<ProductStorageProfile?> FindAsync(int productId, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("""
            SELECT p.ProductID,p.ProductCode,p.ProductName,p.CategoryID,c.CategoryName,
                p.BrandID,b.BrandName,p.Status,b.Status,p.LengthCm,p.WidthCm,p.HeightCm,
                p.GrossWeightKg,p.RowVersion
            FROM dbo.Product p
            JOIN dbo.Category c ON c.CategoryID=p.CategoryID
            JOIN dbo.Brand b ON b.BrandID=p.BrandID
            WHERE p.ProductID=@productId;
            """, connection);
        command.Parameters.Add("@productId", SqlDbType.Int).Value = productId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
            reader.GetInt32(3), reader.GetString(4), reader.GetInt32(5), reader.GetString(6),
            reader.GetString(7), reader.GetString(8), reader.GetDecimal(9), reader.GetDecimal(10),
            reader.GetDecimal(11), reader.GetDecimal(12), Convert.ToBase64String((byte[])reader[13]));
    }
}
