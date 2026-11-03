namespace Inventory.Models.Inbound;
public sealed record CreateReceipt(long PurchaseOrderId, string? Note, Guid Token);
