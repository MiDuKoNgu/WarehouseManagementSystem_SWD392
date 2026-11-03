using System.Text.RegularExpressions;
using Catalog.Models.Products;

namespace Catalog.BLL.Products;
public sealed class ProductNotFoundException() : Exception("Không tìm thấy sản phẩm.");
