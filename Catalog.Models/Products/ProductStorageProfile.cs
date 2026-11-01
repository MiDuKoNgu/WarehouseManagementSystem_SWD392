namespace Catalog.Models.Products;

public sealed record ProductStorageProfile(
    int ProductId, string ProductCode, string ProductName,
    int CategoryId, string CategoryName, int BrandId, string BrandName,
    string Status, string BrandStatus, decimal LengthCm, decimal WidthCm,
    decimal HeightCm, decimal GrossWeightKg, string RowVersion);
