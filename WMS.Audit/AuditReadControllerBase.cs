using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WMS.Audit;

[ApiController]
[Authorize(Roles = "ADMIN")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class AuditReadControllerBase(SqlAuditReader reader) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AuditQuery query, CancellationToken ct)
    {
        try { return Ok(await reader.PageAsync(query, ct)); }
        catch (AuditValidationException error) { return BadRequest(new { message = error.Message, errors = error.Errors }); }
    }

    [HttpGet("actions")]
    public async Task<IActionResult> Actions(CancellationToken ct) => Ok(await reader.ActionsAsync(ct));

    [HttpGet("{id:long:min(1)}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
    {
        var item = await reader.DetailAsync(id, ct);
        return item is null ? NotFound(new { message = "Không tìm thấy lịch sử thao tác." }) : Ok(item);
    }
}
