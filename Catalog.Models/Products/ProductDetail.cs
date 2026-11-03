namespace Catalog.Models.Products;
public sealed record ProductDetail(int ProductId, string ProductCode, string ProductName, string Model, string Unit,
    int CategoryId, string CategoryName, int BrandId, string BrandName, string Status,
    string? Specifications, decimal LengthCm, decimal WidthCm, decimal HeightCm, decimal GrossWeightKg,
    ProductSupplier[] Suppliers, int CreatedByUserId, DateTime CreatedAt, DateTime? UpdatedAt, string RowVersion)
    : ProductSummary(ProductId, ProductCode, ProductName, Model, Unit, CategoryId, CategoryName, BrandId, BrandName, Status);
