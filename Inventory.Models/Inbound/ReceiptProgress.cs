namespace Inventory.Models.Inbound;
public sealed record ReceiptProgress(long PoLineId, int ProductId, string ProductCode, string ProductName,
    int OrderedQuantity, int ClosedQuantity, int ReceivedQuantity, int ReservedQuantity, int CurrentQuantity, int RemainingQuantity);
