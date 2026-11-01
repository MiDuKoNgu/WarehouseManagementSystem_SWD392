using Catalog.DAL.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.API.Controllers;

[ApiController, Route("api/catalog/options")]
[Authorize(Roles = "PURCHASING_STAFF,ADMIN,WAREHOUSE_STAFF")]
public sealed class CatalogOptionsController(CatalogLookupRepository catalog) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(await catalog.GetAsync(cancellationToken));
}
