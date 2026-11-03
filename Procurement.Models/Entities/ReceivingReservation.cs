namespace Procurement.Models.Entities;

public sealed class ReceivingReservation
{
    public Guid Token { get; set; }
    public long PurchaseOrderId { get; set; }
    public int WarehouseId { get; set; }
    public int CreatedByUserId { get; set; }
    public string Status { get; set; } = "OPEN";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}
