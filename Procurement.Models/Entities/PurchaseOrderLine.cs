namespace Procurement.Models.Entities;

public sealed class PurchaseOrderLine
{
    public long PoLineId { get; set; }
    public long PurchaseOrderId { get; set; }
    public int ProductId { get; set; }
    public int OrderedQuantity { get; set; }
    public int ClosedQuantity { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}
