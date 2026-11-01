namespace Procurement.Models.Receiving;

public sealed record ReceivingOrder(long PurchaseOrderId, string PoCode, int SupplierId, string SupplierName,
    int WarehouseId, string WarehouseName, string Status, ReceivingOrderLine[] Lines);
public sealed record ReceivingOrderLine(long PoLineId, int ProductId, string ProductCode, string ProductName,
    int OrderedQuantity, int ClosedQuantity);
public sealed record ReceivingReservationRequest(Guid Token);
public sealed record ReceivingReservationStatus(bool Exists, bool Terminal, long PurchaseOrderId, int WarehouseId);
