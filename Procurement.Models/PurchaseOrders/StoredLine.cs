namespace Procurement.Models.PurchaseOrders;
public sealed record StoredLine(long PoLineId, int ProductId, int Quantity, int ClosedQuantity = 0);
