namespace Procurement.Models.PurchaseOrders;
public sealed record ProductOption(int ProductId, string ProductCode, string ProductName, string Unit, int[] SupplierIds);
