namespace Inventory.Models.Inbound;
public sealed record ReceiptDetail(ReceiptHeader Receipt, ReceiptOrder Order, ReceiptProgress[] Lines, ReceiptItem[] Items, ReceiptPolicy Policy);
