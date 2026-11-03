namespace Catalog.Models.Entities;

public sealed class Product
{
    public int ProductId { get; set; }
    public int CategoryId { get; set; }
    public int BrandId { get; set; }
    public int CreatedByUserId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Model { get; set; } = "";
    public string Unit { get; set; } = "";
    public string? Specifications { get; set; }
    public decimal LengthCm { get; set; }
    public decimal WidthCm { get; set; }
    public decimal HeightCm { get; set; }
    public decimal GrossWeightKg { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Category Category { get; set; } = null!;
    public Brand Brand { get; set; } = null!;
    public ICollection<SupplierProduct> SupplierProducts { get; set; } = new List<SupplierProduct>();
}
