namespace Catalog.Models.Entities;

public sealed class Supplier
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Status { get; set; } = "ACTIVE";
}
