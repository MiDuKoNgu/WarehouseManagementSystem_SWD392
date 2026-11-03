namespace Inventory.Models.Inbound;
public sealed record ReceiptListItem(ReceiptHeader Receipt, string PoCode, string SupplierName, string WarehouseName, long ExpectedQuantity);
