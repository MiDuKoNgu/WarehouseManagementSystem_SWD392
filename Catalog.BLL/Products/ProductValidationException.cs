using System.Text.RegularExpressions;
using Catalog.Models.Products;

namespace Catalog.BLL.Products;
public sealed class ProductValidationException(string[] errors) : Exception(errors.FirstOrDefault() ?? "Dữ liệu sản phẩm không hợp lệ.")
{ public string[] Errors { get; } = errors; }
