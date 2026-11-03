namespace Procurement.Models.PurchaseOrders;
public sealed record ReceivingSummary(long PurchaseOrderId, bool HasAnyInbound, int OpenInboundCount, ReceivedLine[] Lines, bool HasInvalidReceipts = false);
