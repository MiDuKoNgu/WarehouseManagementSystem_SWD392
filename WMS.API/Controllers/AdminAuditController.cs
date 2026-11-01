using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.API.Audit;
using WMS.Audit;

namespace WMS.API.Controllers;

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Roles = "ADMIN")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminAuditController(AuditGateway audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AuditQuery query, CancellationToken ct) => Ok(await audit.ListAsync(query, ct));

    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct) => Ok(await audit.OptionsAsync(ct));

    [HttpGet("{source}/{id:long}")]
    public async Task<IActionResult> Detail(string source, long id, CancellationToken ct)
    {
        var detail = await audit.DetailAsync(source, id, ct);
        return detail is null ? NotFound(new { message = "Không tìm thấy bản ghi Audit Log." }) : Ok(detail);
    }
}
