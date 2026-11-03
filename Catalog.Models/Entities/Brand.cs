namespace Catalog.Models.Entities;

public sealed class Brand
{
    public int BrandId { get; set; }
    public string BrandName { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
}
