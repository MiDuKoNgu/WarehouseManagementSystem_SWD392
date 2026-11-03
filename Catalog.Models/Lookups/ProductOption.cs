namespace Catalog.Models.Lookups;
public sealed record ProductOption(int ProductId, string ProductCode, string ProductName, string Unit, List<int> SupplierIds);
