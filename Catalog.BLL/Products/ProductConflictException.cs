using System.Text.RegularExpressions;
using Catalog.Models.Products;

namespace Catalog.BLL.Products;
public sealed class ProductConflictException(string message) : Exception(message);
