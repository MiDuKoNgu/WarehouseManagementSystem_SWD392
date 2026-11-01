using Warehouse.DAL.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Warehouse.API.Controllers;

[ApiController, Route("api/warehouses/options")]
[Authorize(Roles = "PURCHASING_STAFF,ADMIN,WAREHOUSE_STAFF")]
public sealed class WarehouseOptionsController(WarehouseLookupRepository warehouses) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(await warehouses.GetAsync(cancellationToken));
}
