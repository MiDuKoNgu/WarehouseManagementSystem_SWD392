namespace Inventory.Models.Receiving;

public sealed record ReceivingLine(long PoLineId, int ReceivedQuantity);
public sealed record PurchaseOrderReceivingSummary(long PurchaseOrderId, bool HasAnyInbound, int OpenInboundCount, ReceivingLine[] Lines, bool HasInvalidReceipts = false);
