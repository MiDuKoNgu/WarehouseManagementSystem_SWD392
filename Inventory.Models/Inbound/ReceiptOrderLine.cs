namespace Inventory.Models.Inbound;
public sealed record ReceiptOrderLine(long PoLineId, int ProductId, string ProductCode, string ProductName, int OrderedQuantity, int ClosedQuantity);
