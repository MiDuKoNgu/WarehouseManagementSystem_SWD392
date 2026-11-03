namespace Catalog.Models.Products;
public sealed record ProductPage(ProductSummary[] Items, int TotalCount, int Page, int PageSize);
