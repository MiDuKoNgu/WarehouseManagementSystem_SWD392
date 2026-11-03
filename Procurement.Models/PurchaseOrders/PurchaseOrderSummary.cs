namespace Procurement.Models.PurchaseOrders;
public sealed record PurchaseOrderSummary(long PurchaseOrderId, string PoCode, int SupplierId, string SupplierName,
    int WarehouseId, string WarehouseName, DateOnly OrderDate, DateOnly? ExpectedArrivalDate, string Status,
    DateTime CreatedAt, int LineCount, long TotalQuantity);
