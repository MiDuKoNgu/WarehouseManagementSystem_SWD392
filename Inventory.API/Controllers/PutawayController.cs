using System.Security.Claims;
using Inventory.BLL.Putaway;
using Inventory.Models.Putaway;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.ServiceDefaults;

namespace Inventory.API.Controllers;

[ApiController, Route("api/putaway"), Authorize(Roles = "WAREHOUSE_STAFF")]
public sealed class PutawayController(PutawayService service) : ControllerBase
{
    private PutawayActor Actor => int.TryParse(User.FindFirstValue("warehouse_id"), out var warehouse) && warehouse > 0
        && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var user) && user > 0
        ? new(user, User.FindFirstValue("full_name") ?? User.Identity?.Name ?? user.ToString(), warehouse)
        : throw new PutawayException(403, "WAREHOUSE_REQUIRED", "Tài khoản chưa được phân công kho.");

    [HttpGet("pending")]
    public async Task<IActionResult> Pending(CancellationToken ct, string? search = null, int page = 1, int pageSize = 25)
        => Ok(await service.PendingAsync(Actor, search, page, pageSize, ct));
    [HttpGet("serials/{serialNumber}")]
    public async Task<IActionResult> Scan(string serialNumber, CancellationToken ct)
        => Ok(await service.FindAsync(Actor, null, serialNumber, ct));
    [HttpGet("serials")]
    public async Task<IActionResult> ScanQuery([FromQuery] string serialNumber, CancellationToken ct)
        => Ok(await service.FindAsync(Actor, null, serialNumber, ct));
    [HttpGet("{id:long:min(1)}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
        => Ok(await service.FindAsync(Actor, id, null, ct));
    [HttpGet("{id:long:min(1)}/suggestions")]
    public async Task<IActionResult> Suggestions(long id, CancellationToken ct)
        => Ok(await service.SuggestionsAsync(Actor, id, ct));
    [HttpPost, ValidateSessionCsrf]
    public async Task<IActionResult> Confirm(ConfirmPutaway request, CancellationToken ct)
        => Ok(await service.ConfirmAsync(Actor, request, ct));
}
