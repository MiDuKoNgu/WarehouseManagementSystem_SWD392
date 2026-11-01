using Inventory.DAL.Receiving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.API.Controllers;

[ApiController,Route("api/inventory/purchase-orders"),Authorize(Roles="PURCHASING_STAFF")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class ReceivingSummaryController(IReceivingSummaryRepository receiving) : ControllerBase
{
    [HttpGet("{id:long:min(1)}/receiving-summary")]
    public async Task<IActionResult> Get(long id,CancellationToken ct) => Ok(await receiving.GetAsync(id,ct));
}
