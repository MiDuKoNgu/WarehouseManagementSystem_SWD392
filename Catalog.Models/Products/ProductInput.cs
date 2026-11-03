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
