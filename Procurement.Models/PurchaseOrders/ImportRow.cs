namespace Procurement.Models.PurchaseOrders;

public sealed class ImportRow
{
    public int RowNumber { get; init; }
    public int? SupplierId { get; init; }
    public int? WarehouseId { get; init; }
    public DateOnly? ExpectedArrivalDate { get; init; }
    public string? Note { get; init; }
    public string ProductCode { get; init; } = "";
    public string? ProductName { get; set; }
    public int? Quantity { get; init; }
    public List<string> Errors { get; init; } = [];
}
