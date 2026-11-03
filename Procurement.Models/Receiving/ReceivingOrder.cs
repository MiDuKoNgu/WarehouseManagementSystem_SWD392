namespace Procurement.Models.Receiving;

public sealed record ReceivingOrder(long PurchaseOrderId, string PoCode, int SupplierId, string SupplierName,
    int WarehouseId, string WarehouseName, string Status, ReceivingOrderLine[] Lines);
