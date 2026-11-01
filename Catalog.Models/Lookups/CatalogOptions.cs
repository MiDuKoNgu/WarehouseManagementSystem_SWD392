namespace Catalog.Models.Lookups;

public sealed record SupplierOption(int SupplierId, string SupplierName);
public sealed record ProductOption(int ProductId, string ProductCode, string ProductName, string Unit, List<int> SupplierIds);
public sealed record CatalogOptions(List<SupplierOption> Suppliers, List<ProductOption> Products);
