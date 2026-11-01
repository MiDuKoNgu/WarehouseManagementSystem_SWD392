using Catalog.DAL.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.API.Controllers;

[ApiController, Route("api/catalog/storage-profiles")]
[Authorize(Roles = "WAREHOUSE_STAFF")]
public sealed class ProductStorageProfilesController(ProductStorageProfileRepository profiles) : ControllerBase
{
    [HttpGet("{productId:int:min(1)}")]
    public async Task<IActionResult> Get(int productId, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirst("warehouse_id")?.Value, out var warehouseId) || warehouseId <= 0)
            return StatusCode(403, new { message = "Tài khoản chưa được phân công kho." });
        var profile = await profiles.FindAsync(productId, ct);
        return profile is null ? NotFound(new { message = "Không tìm thấy sản phẩm." }) : Ok(profile);
    }
}
