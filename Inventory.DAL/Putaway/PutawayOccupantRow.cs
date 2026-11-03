namespace Inventory.DAL.Putaway;

internal sealed class PutawayOccupantRow
{
    public long SerialItemID { get; set; }
    public int CurrentLocationID { get; set; }
    public int ProductID { get; set; }
    public string ItemCondition { get; set; } = "";
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
