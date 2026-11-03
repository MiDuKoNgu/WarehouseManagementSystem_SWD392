namespace Procurement.Models.Entities;

public sealed class PurchaseOrder
{
    public long PurchaseOrderId { get; set; }
    public string PoCode { get; set; } = "";
    public int SupplierId { get; set; }
    public int WarehouseId { get; set; }
    public int CreatedByUserId { get; set; }
    public DateOnly OrderDate { get; set; }
    public DateOnly? ExpectedArrivalDate { get; set; }
    public string Status { get; set; } = "DRAFT";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? CloseReason { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
    public ICollection<ReceivingReservation> ReceivingReservations { get; set; } = new List<ReceivingReservation>();
}
