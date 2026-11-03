namespace Inventory.Models.Inbound;
public sealed record ReceiptHeader(long InboundId, long PurchaseOrderId, string ReceiptCode, string Status, DateOnly ReceiptDate,
    DateTime CreatedAt, int CreatedByUserId, string? CreatedByName, DateTime? CompletedAt, int? PerformedByUserId,
    string? PerformedByName, string? Note, string RowVersion, int WarehouseId, int SerialCount);
