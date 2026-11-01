using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.DAL.Repositories;

namespace Warehouse.API.Controllers;

[ApiController, Route("api/warehouses/putaway-layout")]
[Authorize(Roles = "WAREHOUSE_STAFF")]
public sealed class PutawayLayoutController(PutawayLayoutRepository layouts) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirst("warehouse_id")?.Value, out var warehouseId) || warehouseId <= 0)
            return StatusCode(403, new { message = "Tài khoản chưa được phân công kho." });
        var layout = await layouts.GetAsync(warehouseId, ct);
        return layout is null ? NotFound(new { message = "Không tìm thấy kho được phân công." }) : Ok(layout);
    }
}
