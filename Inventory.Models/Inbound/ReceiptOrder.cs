namespace Inventory.Models.Inbound;
public sealed record ReceiptOrder(long PurchaseOrderId, string PoCode, int SupplierId, string SupplierName, int WarehouseId,
    string WarehouseName, string Status, ReceiptOrderLine[] Lines);
