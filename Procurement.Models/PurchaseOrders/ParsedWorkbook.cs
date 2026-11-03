namespace Procurement.Models.PurchaseOrders;
public sealed record ParsedWorkbook(ImportRow[] Rows, string[] Errors);
