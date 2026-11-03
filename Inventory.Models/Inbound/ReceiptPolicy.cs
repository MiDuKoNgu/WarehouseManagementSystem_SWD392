namespace Inventory.Models.Inbound;
public sealed record ReceiptPolicy(bool AllowMultipleDrafts = false, bool AcceptDamaged = false, bool AllowCancellation = false);
