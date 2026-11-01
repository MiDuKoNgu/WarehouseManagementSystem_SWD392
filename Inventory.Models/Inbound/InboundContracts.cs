namespace Inventory.Models.Inbound;

public sealed record ReceiptActor(int UserId, string Name, int WarehouseId);
public sealed record ReceiptPolicy(bool AllowMultipleDrafts = false, bool AcceptDamaged = false, bool AllowCancellation = false);
public sealed record ReceiptOrder(long PurchaseOrderId, string PoCode, int SupplierId, string SupplierName, int WarehouseId,
    string WarehouseName, string Status, ReceiptOrderLine[] Lines);
public sealed record ReceiptOrderLine(long PoLineId, int ProductId, string ProductCode, string ProductName, int OrderedQuantity, int ClosedQuantity);
public sealed record ReceiptHeader(long InboundId, long PurchaseOrderId, string ReceiptCode, string Status, DateOnly ReceiptDate,
    DateTime CreatedAt, int CreatedByUserId, string? CreatedByName, DateTime? CompletedAt, int? PerformedByUserId,
    string? PerformedByName, string? Note, string RowVersion, int WarehouseId, int SerialCount);
public sealed record ReceiptItem(long InboundItemId, long PoLineId, string ScannedSerial, int? ManufacturingMonth,
    int? ManufacturingYear, string? ReceivedCondition, string? ConditionNote, string InputMethod, int? ScannedByUserId,
    string? ScannedByName, DateTime ScannedAt, long? SerialItemId);
public sealed record ReceiptProgress(long PoLineId, int ProductId, string ProductCode, string ProductName,
    int OrderedQuantity, int ClosedQuantity, int ReceivedQuantity, int ReservedQuantity, int CurrentQuantity, int RemainingQuantity);
public sealed record ReceiptDetail(ReceiptHeader Receipt, ReceiptOrder Order, ReceiptProgress[] Lines, ReceiptItem[] Items, ReceiptPolicy Policy);
public sealed record ReceiptListItem(ReceiptHeader Receipt, string PoCode, string SupplierName, string WarehouseName, long ExpectedQuantity);
public sealed record CreateReceipt(long PurchaseOrderId, string? Note, Guid Token);
public sealed record SaveReceiptItem(long PoLineId, string Serial, int ManufacturingMonth, int ManufacturingYear,
    string ReceivedCondition, string? ConditionNote, string InputMethod, string RowVersion);
public sealed record ReceiptTransition(string RowVersion, string? Reason = null);
