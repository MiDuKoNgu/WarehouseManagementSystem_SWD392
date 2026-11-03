namespace Inventory.Models.Inbound;
public sealed record ReceiptTransition(string RowVersion, string? Reason = null);
