namespace Procurement.Models.PurchaseOrders;

// Database-only records are enriched with current service lookup labels in BLL.
public sealed record StoredPurchaseOrder(long PurchaseOrderId, string PoCode, int SupplierId, int WarehouseId,
    DateOnly OrderDate, DateOnly? ExpectedArrivalDate, string Status, DateTime CreatedAt, int CreatedByUserId,
    string? Note, StoredLine[] Lines, string RowVersion = "", DateTime? ConfirmedAt = null,
    DateTime? ClosedAt = null, string? CloseReason = null);
