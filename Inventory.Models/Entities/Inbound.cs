namespace Inventory.Models.Entities;

public sealed class Inbound
{
    public int? WarehouseId { get; set; }
    public string? CreatedByName { get; set; }
    public string? PerformedByName { get; set; }
    public Guid? CreationToken { get; set; }
    public List<InboundItem> Items { get; set; } = [];
    public long InboundId { get; set; }
    public long PurchaseOrderId { get; set; }
    public int CreatedByUserId { get; set; }
    public int? PerformedByUserId { get; set; }
    public string ReceiptCode { get; set; } = "";
    public DateOnly ReceiptDate { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Note { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
