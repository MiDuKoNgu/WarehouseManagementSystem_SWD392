namespace Procurement.Models.PurchaseOrders;
public sealed record PurchaseOrderOptions(SupplierOption[] Suppliers, WarehouseOption[] Warehouses, ProductOption[] Products);
