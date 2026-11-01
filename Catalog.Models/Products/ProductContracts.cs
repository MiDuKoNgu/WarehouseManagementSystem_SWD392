namespace Catalog.Models.Products;

public sealed class ProductInput
{
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Model { get; set; } = "";
    public string Unit { get; set; } = "";
    public int CategoryId { get; set; }
    public int BrandId { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string? Specifications { get; set; }
    public decimal LengthCm { get; set; }
    public decimal WidthCm { get; set; }
    public decimal HeightCm { get; set; }
    public decimal GrossWeightKg { get; set; }
    public int[] SupplierIds { get; set; } = [];
    public string? RowVersion { get; set; }
}
public sealed class ProductQuery
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public int? BrandId { get; set; }
    public int? SupplierId { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
public sealed record ProductCategory(int CategoryId, string CategoryName);
public sealed record ProductBrand(int BrandId, string BrandName);
public sealed record ProductSupplier(int SupplierId, string SupplierName);
public sealed record ProductOptions(ProductCategory[] Categories, ProductBrand[] Brands, ProductSupplier[] Suppliers);
public record ProductSummary(int ProductId, string ProductCode, string ProductName, string Model, string Unit,
    int CategoryId, string CategoryName, int BrandId, string BrandName, string Status);
public sealed record ProductDetail(int ProductId, string ProductCode, string ProductName, string Model, string Unit,
    int CategoryId, string CategoryName, int BrandId, string BrandName, string Status,
    string? Specifications, decimal LengthCm, decimal WidthCm, decimal HeightCm, decimal GrossWeightKg,
    ProductSupplier[] Suppliers, int CreatedByUserId, DateTime CreatedAt, DateTime? UpdatedAt, string RowVersion)
    : ProductSummary(ProductId, ProductCode, ProductName, Model, Unit, CategoryId, CategoryName, BrandId, BrandName, Status);
public sealed record ProductPage(ProductSummary[] Items, int TotalCount, int Page, int PageSize);
