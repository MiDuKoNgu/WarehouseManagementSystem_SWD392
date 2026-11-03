namespace Procurement.Models.Receiving;
public sealed record ReceivingOrderLine(long PoLineId, int ProductId, string ProductCode, string ProductName,
    int OrderedQuantity, int ClosedQuantity);
