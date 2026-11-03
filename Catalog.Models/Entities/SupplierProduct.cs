namespace Catalog.Models.Entities;

public sealed class SupplierProduct
{
    public int SupplierId { get; set; }
    public int ProductId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
