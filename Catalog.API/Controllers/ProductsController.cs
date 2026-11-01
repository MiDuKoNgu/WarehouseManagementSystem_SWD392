using System.Security.Claims;
using Catalog.BLL.Products;
using Catalog.Models.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.ServiceDefaults;

namespace Catalog.API.Controllers;

[ApiController, Route("api/products"), Authorize(Roles = "PURCHASING_STAFF")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProductsController(ProductService products) : ControllerBase
{
    private int ActorId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet] public async Task<IActionResult> List([FromQuery] ProductQuery query, CancellationToken ct) => Ok(await products.ListAsync(query, ct));
    [HttpGet("options")] public async Task<IActionResult> Options(CancellationToken ct) => Ok(await products.OptionsAsync(ct));
    [HttpGet("{id:int:min(1)}")] public async Task<IActionResult> Detail(int id, CancellationToken ct)
    { var product = await products.FindAsync(id, ct); return product is null ? NotFound(new { message = "Không tìm thấy sản phẩm." }) : Ok(product); }
    [HttpPost, ValidateSessionCsrf] public async Task<IActionResult> Create(ProductInput input, CancellationToken ct)
    { var product = await products.CreateAsync(input, ActorId, ct); return CreatedAtAction(nameof(Detail), new { id = product.ProductId }, product); }
    [HttpPut("{id:int:min(1)}"), ValidateSessionCsrf] public async Task<IActionResult> Update(int id, ProductInput input, CancellationToken ct) => Ok(await products.UpdateAsync(id, input, ActorId, ct));
}
