using System.Data;
using Catalog.BLL.Products;
using Catalog.Models.Products;
using Microsoft.Data.SqlClient;

namespace Catalog.DAL.Repositories;

public sealed class SqlProductRepository(string connectionString) : IProductRepository
{
    private const string Select = """
        SELECT p.ProductID,p.ProductCode,p.ProductName,p.Model,p.Unit,p.CategoryID,c.CategoryName,
            p.BrandID,b.BrandName,p.Status,p.Specifications,p.LengthCm,p.WidthCm,p.HeightCm,p.GrossWeightKg,
            p.CreatedByUserID,p.CreatedAt,p.UpdatedAt,p.RowVersion
        FROM dbo.Product p JOIN dbo.Category c ON c.CategoryID=p.CategoryID JOIN dbo.Brand b ON b.BrandID=p.BrandID
        """;
    private const string Filter = """
        WHERE (@search IS NULL OR p.ProductCode LIKE @search ESCAPE '~' OR p.ProductName LIKE @search ESCAPE '~' OR p.Model LIKE @search ESCAPE '~')
          AND (@category IS NULL OR p.CategoryID=@category) AND (@brand IS NULL OR p.BrandID=@brand)
          AND (@status IS NULL OR p.Status=@status)
          AND (@supplier IS NULL OR EXISTS (SELECT 1 FROM dbo.SupplierProduct sp WHERE sp.ProductID=p.ProductID AND sp.SupplierID=@supplier))
        """;

    public async Task<ProductPage> ListAsync(ProductQuery query, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var count = new SqlCommand("SELECT COUNT(*) FROM dbo.Product p " + Filter, connection);
        FilterParameters(count, query);
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct));
        await using var command = new SqlCommand(Select + "\n" + Filter + "\n ORDER BY p.ProductCode,p.ProductID OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY", connection);
        FilterParameters(command, query);
        command.Parameters.Add("@skip", SqlDbType.Int).Value = checked((query.Page - 1) * query.PageSize);
        command.Parameters.Add("@take", SqlDbType.Int).Value = query.PageSize;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var items = new List<ProductSummary>();
        while (await reader.ReadAsync(ct)) items.Add(Summary(reader));
        return new(items.ToArray(), total, query.Page, query.PageSize);
    }

    public async Task<ProductDetail?> FindAsync(int id, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return await ReadDetail(connection, null, id, ct);
    }

    public async Task<ProductOptions> OptionsAsync(CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("""
            SELECT CategoryID,CategoryName FROM dbo.Category ORDER BY CategoryName;
            SELECT BrandID,BrandName FROM dbo.Brand WHERE Status='ACTIVE' ORDER BY BrandName;
            SELECT SupplierID,SupplierName FROM dbo.Supplier WHERE Status='ACTIVE' ORDER BY SupplierName;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var categories = new List<ProductCategory>(); var brands = new List<ProductBrand>(); var suppliers = new List<ProductSupplier>();
        while (await reader.ReadAsync(ct)) categories.Add(new(reader.GetInt32(0), reader.GetString(1)));
        await reader.NextResultAsync(ct);
        while (await reader.ReadAsync(ct)) brands.Add(new(reader.GetInt32(0), reader.GetString(1)));
        await reader.NextResultAsync(ct);
        while (await reader.ReadAsync(ct)) suppliers.Add(new(reader.GetInt32(0), reader.GetString(1)));
        return new(categories.ToArray(), brands.ToArray(), suppliers.ToArray());
    }

    public async Task<ProductDetail> SaveAsync(int? id, ProductInput input, int actorId, byte[]? rowVersion, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            if (id is { } productId)
            {
                await using var current = new SqlCommand("SELECT ProductCode,RowVersion FROM dbo.Product WITH (UPDLOCK,HOLDLOCK) WHERE ProductID=@id", connection, transaction);
                current.Parameters.Add("@id", SqlDbType.Int).Value = productId;
                await using var reader = await current.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct)) throw new ProductNotFoundException();
                if (!reader.GetString(0).Equals(input.ProductCode, StringComparison.OrdinalIgnoreCase))
                    throw new ProductValidationException(["Không được thay đổi mã sản phẩm sau khi tạo."]);
                if (rowVersion is null || !((byte[])reader[1]).SequenceEqual(rowVersion))
                    throw new ProductConflictException("Sản phẩm đã được người khác sửa. Hãy tải lại trang trước khi lưu.");
            }

            await using var references = new SqlCommand("""
                SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.Category WITH (UPDLOCK,HOLDLOCK) WHERE CategoryID=@category)
                    AND EXISTS(SELECT 1 FROM dbo.Brand WITH (UPDLOCK,HOLDLOCK) WHERE BrandID=@brand AND Status='ACTIVE') THEN 1 ELSE 0 END
                """, connection, transaction);
            references.Parameters.Add("@category", SqlDbType.Int).Value = input.CategoryId;
            references.Parameters.Add("@brand", SqlDbType.Int).Value = input.BrandId;
            if (Convert.ToInt32(await references.ExecuteScalarAsync(ct)) != 1)
                throw new ProductValidationException(["Danh mục không tồn tại hoặc hãng không hoạt động."]);
            var supplierNames = input.SupplierIds.Select((_, index) => "@s" + index).ToArray();
            await using var supplierCheck = new SqlCommand("SELECT COUNT(*) FROM dbo.Supplier WITH (UPDLOCK,HOLDLOCK) WHERE Status='ACTIVE' AND SupplierID IN (" + string.Join(",", supplierNames) + ")", connection, transaction);
            for (var index = 0; index < input.SupplierIds.Length; index++) supplierCheck.Parameters.Add(supplierNames[index], SqlDbType.Int).Value = input.SupplierIds[index];
            if (Convert.ToInt32(await supplierCheck.ExecuteScalarAsync(ct)) != input.SupplierIds.Length)
                throw new ProductValidationException(["Một hoặc nhiều nhà cung cấp không tồn tại hoặc không hoạt động."]);

            var sql = id is null ? """
                INSERT dbo.Product(CategoryID,BrandID,CreatedByUserID,ProductCode,ProductName,Model,Unit,Specifications,
                    LengthCm,WidthCm,HeightCm,GrossWeightKg,Status,CreatedAt)
                OUTPUT INSERTED.ProductID
                VALUES(@category,@brand,@actor,@code,@name,@model,@unit,@specs,@length,@width,@height,@weight,@status,SYSUTCDATETIME())
                """ : """
                UPDATE dbo.Product SET CategoryID=@category,BrandID=@brand,ProductName=@name,Model=@model,Unit=@unit,
                    Specifications=@specs,LengthCm=@length,WidthCm=@width,HeightCm=@height,GrossWeightKg=@weight,
                    Status=@status,UpdatedAt=SYSUTCDATETIME()
                OUTPUT INSERTED.ProductID WHERE ProductID=@id AND RowVersion=@version
                """;
            await using var save = new SqlCommand(sql, connection, transaction);
            ProductParameters(save, input, actorId);
            if (id is not null)
            {
                save.Parameters.Add("@id", SqlDbType.Int).Value = id;
                save.Parameters.Add("@version", SqlDbType.Timestamp, 8).Value = rowVersion!;
            }
            var output = await save.ExecuteScalarAsync(ct);
            if (output is null) throw new ProductConflictException("Phiên bản sản phẩm đã thay đổi. Hãy tải lại trang.");
            var savedId = Convert.ToInt32(output);
            if (id is not null)
            {
                await using var remove = new SqlCommand("DELETE FROM dbo.SupplierProduct WHERE ProductID=@id", connection, transaction);
                remove.Parameters.Add("@id", SqlDbType.Int).Value = savedId;
                await remove.ExecuteNonQueryAsync(ct);
            }
            foreach (var supplierId in input.SupplierIds)
            {
                await using var add = new SqlCommand("INSERT dbo.SupplierProduct(SupplierID,ProductID) VALUES(@supplier,@product)", connection, transaction);
                add.Parameters.Add("@supplier", SqlDbType.Int).Value = supplierId;
                add.Parameters.Add("@product", SqlDbType.Int).Value = savedId;
                await add.ExecuteNonQueryAsync(ct);
            }
            var detail = await ReadDetail(connection, transaction, savedId, ct) ?? throw new InvalidOperationException("Saved product is missing.");
            await transaction.CommitAsync(ct);
            return detail;
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        { throw new ProductConflictException("Mã sản phẩm đã tồn tại. Vui lòng chọn mã khác."); }
    }

    private static async Task<ProductDetail?> ReadDetail(SqlConnection connection, SqlTransaction? transaction, int id, CancellationToken ct)
    {
        await using var command = new SqlCommand(Select + " WHERE p.ProductID=@id; SELECT s.SupplierID,s.SupplierName FROM dbo.SupplierProduct sp JOIN dbo.Supplier s ON s.SupplierID=sp.SupplierID WHERE sp.ProductID=@id ORDER BY s.SupplierName", connection, transaction);
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var summary = Summary(reader);
        var specs = reader.IsDBNull(10) ? null : reader.GetString(10);
        var length = reader.GetDecimal(11); var width = reader.GetDecimal(12); var height = reader.GetDecimal(13); var weight = reader.GetDecimal(14);
        var creator = reader.GetInt32(15); var created = DateTime.SpecifyKind(reader.GetDateTime(16), DateTimeKind.Utc);
        var updated = reader.IsDBNull(17) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(17), DateTimeKind.Utc);
        var version = Convert.ToBase64String((byte[])reader[18]);
        await reader.NextResultAsync(ct);
        var suppliers = new List<ProductSupplier>();
        while (await reader.ReadAsync(ct)) suppliers.Add(new(reader.GetInt32(0), reader.GetString(1)));
        return new(summary.ProductId, summary.ProductCode, summary.ProductName, summary.Model, summary.Unit,
            summary.CategoryId, summary.CategoryName, summary.BrandId, summary.BrandName, summary.Status,
            specs, length, width, height, weight, suppliers.ToArray(), creator, created, updated, version);
    }
    private static ProductSummary Summary(SqlDataReader reader) => new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt32(5), reader.GetString(6), reader.GetInt32(7), reader.GetString(8), reader.GetString(9));
    private static void FilterParameters(SqlCommand command, ProductQuery query)
    {
        var search = query.Search?.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[");
        command.Parameters.Add("@search", SqlDbType.NVarChar, 210).Value = search is null ? DBNull.Value : "%" + search + "%";
        command.Parameters.Add("@category", SqlDbType.Int).Value = query.CategoryId is { } category ? category : DBNull.Value;
        command.Parameters.Add("@brand", SqlDbType.Int).Value = query.BrandId is { } brand ? brand : DBNull.Value;
        command.Parameters.Add("@supplier", SqlDbType.Int).Value = query.SupplierId is { } supplier ? supplier : DBNull.Value;
        command.Parameters.Add("@status", SqlDbType.VarChar, 32).Value = (object?)query.Status ?? DBNull.Value;
    }
    private static void ProductParameters(SqlCommand command, ProductInput input, int actorId)
    {
        command.Parameters.Add("@category", SqlDbType.Int).Value = input.CategoryId;
        command.Parameters.Add("@brand", SqlDbType.Int).Value = input.BrandId;
        command.Parameters.Add("@actor", SqlDbType.Int).Value = actorId;
        command.Parameters.Add("@code", SqlDbType.VarChar, 40).Value = input.ProductCode;
        command.Parameters.Add("@name", SqlDbType.NVarChar, 200).Value = input.ProductName;
        command.Parameters.Add("@model", SqlDbType.NVarChar, 100).Value = input.Model;
        command.Parameters.Add("@unit", SqlDbType.NVarChar, 30).Value = input.Unit;
        command.Parameters.Add("@specs", SqlDbType.NVarChar, -1).Value = (object?)input.Specifications ?? DBNull.Value;
        command.Parameters.Add("@status", SqlDbType.VarChar, 32).Value = input.Status;
        foreach (var (name, value, scale) in new[] { ("@length",input.LengthCm,(byte)2), ("@width",input.WidthCm,(byte)2), ("@height",input.HeightCm,(byte)2), ("@weight",input.GrossWeightKg,(byte)3) })
        { var parameter = command.Parameters.Add(name, SqlDbType.Decimal); parameter.Precision = 10; parameter.Scale = scale; parameter.Value = value; }
    }
}
