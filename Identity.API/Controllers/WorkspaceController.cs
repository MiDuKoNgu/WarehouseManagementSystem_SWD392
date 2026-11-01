using Identity.Models.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/workspaces")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WorkspaceController : ControllerBase
{
    [HttpGet("admin"), Authorize(Roles = UserRoles.Admin)]
    public IActionResult Admin() => Profile();

    [HttpGet("purchasing"), Authorize(Roles = UserRoles.Purchasing)]
    public IActionResult Purchasing() => Profile();

    [HttpGet("warehouse"), Authorize(Roles = UserRoles.Warehouse)]
    public IActionResult Warehouse() => Profile();

    private IActionResult Profile() => Ok(new
    {
        user = UserProfile.From((AuthUser)HttpContext.Items["AuthUser"]!)
    });
}
