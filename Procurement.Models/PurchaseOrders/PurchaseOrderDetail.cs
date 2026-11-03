namespace Procurement.Models.PurchaseOrders;
public sealed record PurchaseOrderDetail(long PurchaseOrderId, string PoCode, int SupplierId, string SupplierName,
    int WarehouseId, string WarehouseName, DateOnly OrderDate, DateOnly? ExpectedArrivalDate, string Status,
    DateTime CreatedAt, int LineCount, long TotalQuantity, int CreatedByUserId, string CreatedByName,
    string? Note, PurchaseOrderLine[] Lines, string RowVersion = "", DateTime? ConfirmedAt = null,
    DateTime? ClosedAt = null, string? CloseReason = null, bool HasAnyInbound = false, int OpenInboundCount = 0);
