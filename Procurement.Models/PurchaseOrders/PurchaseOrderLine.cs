namespace Procurement.Models.PurchaseOrders;
public sealed record PurchaseOrderLine(long PoLineId, int ProductId, string ProductCode, string ProductName, string Unit, int Quantity,
    int ClosedQuantity = 0, int ReceivedQuantity = 0, int RemainingQuantity = 0);
