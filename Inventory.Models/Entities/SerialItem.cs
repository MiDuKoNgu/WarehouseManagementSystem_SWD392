namespace Inventory.Models.Entities;

public sealed class SerialItem
{
    public long SerialItemId { get; set; }
    public int ProductId { get; set; }
    public string SerialNumber { get; set; } = "";
    public byte? ManufacturingMonth { get; set; }
    public short? ManufacturingYear { get; set; }
    public string OriginType { get; set; } = "";
    public long? OriginalInboundId { get; set; }
    public long? OriginAdjustmentId { get; set; }
    public int? CurrentWarehouseId { get; set; }
    public int? CurrentLocationId { get; set; }
    public long? CurrentSupplySourceId { get; set; }
    public string StockState { get; set; } = "";
    public string ItemCondition { get; set; } = "";
    public bool IsQuarantined { get; set; }
    public string? QuarantineReason { get; set; }
    public DateTime RegisteredAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
