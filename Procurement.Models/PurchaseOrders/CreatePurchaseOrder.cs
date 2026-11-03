namespace Procurement.Models.PurchaseOrders;
public sealed record CreatePurchaseOrder(int SupplierId, int WarehouseId, DateOnly? ExpectedArrivalDate, string? Note, CreateLine[] Lines);
