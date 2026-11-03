namespace Inventory.Models.Entities;

public sealed class SupplySource
{
    public long SupplySourceId { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public string ItemCondition { get; set; } = "";
    public string SourceType { get; set; } = "";
    public string SupplyState { get; set; } = "";
    public long? POLineId { get; set; }
    public long? InboundId { get; set; }
    public long? ReturnReceiptId { get; set; }
    public long? TransferReceiptId { get; set; }
    public long? AdjustmentId { get; set; }
    public int EligibleQuantity { get; set; }
    public int RemainingQuantity { get; set; }
    public DateOnly? AvailableFrom { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
