using System.Text.RegularExpressions;
using Catalog.Models.Products;

namespace Catalog.BLL.Products;

public interface IProductRepository
{
    Task<ProductPage> ListAsync(ProductQuery query, CancellationToken ct);
    Task<ProductDetail?> FindAsync(int id, CancellationToken ct);
    Task<ProductOptions> OptionsAsync(CancellationToken ct);
    Task<ProductDetail> SaveAsync(int? id, ProductInput input, int actorId, byte[]? rowVersion, CancellationToken ct);
}
