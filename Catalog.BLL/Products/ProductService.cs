using System.Text.RegularExpressions;
using Catalog.Models.Products;

namespace Catalog.BLL.Products;

public interface IProductRepository
{
    Task<ProductPage> ListAsync(ProductQuery query, CancellationToken ct);
    Task<ProductDetail?> FindAsync(int id, CancellationToken ct);
    Task<ProductOptions> OptionsAsync(CancellationToken ct);
    Task<ProductDetail> SaveAsync(int? id, ProductInput input, int actorId, byte[]? rowVersion, CancellationToken ct);
}
public sealed class ProductValidationException(string[] errors) : Exception(errors.FirstOrDefault() ?? "Dữ liệu sản phẩm không hợp lệ.")
{ public string[] Errors { get; } = errors; }
public sealed class ProductConflictException(string message) : Exception(message);
public sealed class ProductNotFoundException() : Exception("Không tìm thấy sản phẩm.");

public sealed class ProductService(IProductRepository products)
{
    public Task<ProductPage> ListAsync(ProductQuery query, CancellationToken ct)
    {
        query.Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        query.Status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim().ToUpperInvariant();
        var errors = new List<string>();
        if (query.Search?.Length > 100) errors.Add("Từ khóa tối đa 100 ký tự.");
        if (query.Status is not (null or "ACTIVE" or "INACTIVE")) errors.Add("Trạng thái không hợp lệ.");
        if (query.CategoryId is <= 0 || query.BrandId is <= 0 || query.SupplierId is <= 0) errors.Add("Bộ lọc danh mục/hãng/nhà cung cấp không hợp lệ.");
        if (query.Page is < 1 or > 10000 || query.PageSize is < 1 or > 50) errors.Add("Trang cần từ 1 đến 10000, mỗi trang từ 1 đến 50 sản phẩm.");
        if (errors.Count > 0) throw new ProductValidationException(errors.ToArray());
        return products.ListAsync(query, ct);
    }
    public Task<ProductDetail?> FindAsync(int id, CancellationToken ct) => products.FindAsync(id, ct);
    public Task<ProductOptions> OptionsAsync(CancellationToken ct) => products.OptionsAsync(ct);
    public Task<ProductDetail> CreateAsync(ProductInput input, int actorId, CancellationToken ct)
    {
        Validate(input);
        return products.SaveAsync(null, input, actorId, null, ct);
    }
    public Task<ProductDetail> UpdateAsync(int id, ProductInput input, int actorId, CancellationToken ct)
    {
        Validate(input);
        byte[] version;
        try { version = Convert.FromBase64String(input.RowVersion ?? ""); }
        catch (FormatException) { throw new ProductValidationException(["Phiên bản sản phẩm không hợp lệ. Hãy tải lại chi tiết."]); }
        if (version.Length != 8) throw new ProductValidationException(["Thiếu phiên bản sản phẩm. Hãy tải lại chi tiết."]);
        return products.SaveAsync(id, input, actorId, version, ct);
    }
    public static void Validate(ProductInput input)
    {
        input.ProductCode = input.ProductCode?.Trim().ToUpperInvariant() ?? "";
        input.ProductName = input.ProductName?.Trim() ?? "";
        input.Model = input.Model?.Trim() ?? "";
        input.Unit = input.Unit?.Trim() ?? "";
        input.Status = input.Status?.Trim().ToUpperInvariant() ?? "";
        input.Specifications = string.IsNullOrWhiteSpace(input.Specifications) ? null : input.Specifications.Trim();
        var errors = new List<string>();
        if (!Regex.IsMatch(input.ProductCode, @"^[A-Z0-9][A-Z0-9._/-]{0,39}$")) errors.Add("Mã sản phẩm gồm 1–40 ký tự chữ/số, dấu chấm, gạch dưới, gạch ngang hoặc dấu /.");
        if (input.ProductName.Length is < 1 or > 200) errors.Add("Tên sản phẩm cần 1–200 ký tự.");
        if (input.Model.Length is < 1 or > 100) errors.Add("Model cần 1–100 ký tự.");
        if (input.Unit.Length is < 1 or > 30) errors.Add("Đơn vị cần 1–30 ký tự.");
        if (input.Specifications?.Length > 10000) errors.Add("Thông số mô tả tối đa 10000 ký tự.");
        if (input.CategoryId <= 0 || input.BrandId <= 0) errors.Add("Vui lòng chọn danh mục và hãng.");
        if (input.Status is not ("ACTIVE" or "INACTIVE")) errors.Add("Trạng thái chỉ nhận ACTIVE hoặc INACTIVE.");
        if (input.SupplierIds is null || input.SupplierIds.Length is < 1 or > 100 || input.SupplierIds.Any(id => id <= 0)
            || input.SupplierIds.Distinct().Count() != input.SupplierIds.Length) errors.Add("Chọn từ 1 đến 100 nhà cung cấp, không trùng nhau.");
        foreach (var dimension in new[] { input.LengthCm, input.WidthCm, input.HeightCm })
            if (dimension <= 0 || dimension > 99999999.99m || decimal.Round(dimension, 2) != dimension) errors.Add("Kích thước phải lớn hơn 0, tối đa 99999999.99 cm và tối đa 2 số lẻ.");
        if (input.GrossWeightKg <= 0 || input.GrossWeightKg > 9999999.999m || decimal.Round(input.GrossWeightKg, 3) != input.GrossWeightKg)
            errors.Add("Khối lượng phải lớn hơn 0, tối đa 9999999.999 kg và tối đa 3 số lẻ.");
        if (errors.Count > 0) throw new ProductValidationException(errors.Distinct().ToArray());
    }
}
