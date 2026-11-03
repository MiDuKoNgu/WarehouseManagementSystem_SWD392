namespace Procurement.Models.Receiving;
public sealed record ReceivingReservationStatus(bool Exists, bool Terminal, long PurchaseOrderId, int WarehouseId);
