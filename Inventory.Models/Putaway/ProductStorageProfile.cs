namespace Inventory.Models.Putaway;

public sealed record ProductStorageProfile(int ProductId, string ProductCode, string ProductName,
    int CategoryId, string CategoryName, int BrandId, string BrandName, string Status,
    decimal? LengthCm, decimal? WidthCm, decimal? HeightCm, decimal? GrossWeightKg);
