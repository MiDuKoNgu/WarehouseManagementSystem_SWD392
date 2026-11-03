namespace Catalog.Models.Products;
public record ProductSummary(int ProductId, string ProductCode, string ProductName, string Model, string Unit,
    int CategoryId, string CategoryName, int BrandId, string BrandName, string Status);
