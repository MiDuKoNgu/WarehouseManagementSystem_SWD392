namespace Inventory.DAL.Putaway;

// Unmapped EF query projections for locked cross-service reads. Inventory entities own mutations.
internal sealed class PutawaySerialRow
{
    public long SerialItemID { get; set; }
    public string SerialNumber { get; set; } = "";
    public int ProductID { get; set; }
    public string StockState { get; set; } = "";
    public string ItemCondition { get; set; } = "";
    public bool IsQuarantined { get; set; }
    public int? CurrentWarehouseID { get; set; }
    public int? CurrentLocationID { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public long? OriginalInboundID { get; set; }
    public string? ReceiptCode { get; set; }
    public DateTime RegisteredAt { get; set; }
    public string? InboundStatus { get; set; }
    public string? OriginType { get; set; }
    public string? WarehouseName { get; set; }
    public int? ProfileProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public int? CategoryID { get; set; }
    public string? CategoryName { get; set; }
    public int? BrandID { get; set; }
    public string? BrandName { get; set; }
    public string? ProductStatus { get; set; }
    public decimal? LengthCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? GrossWeightKg { get; set; }
}
