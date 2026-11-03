namespace Procurement.Models.PurchaseOrders;
public sealed record ImportPreview(string? Token, bool Valid, ImportRow[] Rows, string[] Errors);
