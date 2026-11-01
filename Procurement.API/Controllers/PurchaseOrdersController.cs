using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procurement.BLL.PurchaseOrders;
using Procurement.Models.PurchaseOrders;
using WMS.ServiceDefaults;

namespace Procurement.API.Controllers;

[ApiController]
[Route("api/purchase-orders")]
[Authorize(Roles = "PURCHASING_STAFF")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PurchaseOrdersController(PurchaseOrderService service) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string UserName => User.FindFirstValue("full_name") ?? User.Identity?.Name ?? $"Người dùng #{UserId}";

    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct) => Ok(await service.OptionsAsync(ct));

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpGet("{id:long:min(1)}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
    {
        var order = await service.FindAsync(id, ct);
        return order is null ? NotFound(new { message = "Không tìm thấy đơn mua hàng." }) : Ok(order);
    }

    [HttpPost]
    [ValidateSessionCsrf]
    public async Task<IActionResult> Create(CreatePurchaseOrder request, CancellationToken ct)
    {
        var order = await service.CreateAsync(request, UserId, UserName, ct);
        return CreatedAtAction(nameof(Detail), new { id = order.PurchaseOrderId }, order);
    }

    [HttpGet("import/template")]
    public async Task<IActionResult> Template(CancellationToken ct) =>
        File(await service.TemplateAsync(ct), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "EWMS-PO-template.xlsx");

    [HttpPut("{id:long:min(1)}")]
    [ValidateSessionCsrf]
    public async Task<IActionResult> Update(long id, UpdatePurchaseOrder request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, UserId, UserName, ct));

    [HttpPost("{id:long:min(1)}/confirm")]
    [ValidateSessionCsrf]
    public async Task<IActionResult> ConfirmOrder(long id, ConfirmPurchaseOrder request, CancellationToken ct) =>
        Ok(await service.ConfirmOrderAsync(id, request.RowVersion, UserId, UserName, ct));

    [HttpPost("{id:long:min(1)}/cancel")]
    [ValidateSessionCsrf]
    public async Task<IActionResult> Cancel(long id, FinishPurchaseOrder request, CancellationToken ct) =>
        Ok(await service.CancelAsync(id, request, UserId, UserName, ct));

    [HttpPost("{id:long:min(1)}/close")]
    [ValidateSessionCsrf]
    public async Task<IActionResult> Close(long id, FinishPurchaseOrder request, CancellationToken ct) =>
        Ok(await service.CloseAsync(id, request, UserId, UserName, ct));

    [HttpPost("import/preview")]
    [ValidateSessionCsrf]
    [RequestSizeLimit(2_162_688)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Preview([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Vui lòng chọn file Excel .xlsx." });
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Chỉ hỗ trợ định dạng .xlsx." });
        if (file.Length > 2_097_152) return BadRequest(new { message = "File Excel tối đa 2 MB." });
        await using var stream = file.OpenReadStream();
        return Ok(await service.PreviewAsync(stream, UserId, ct));
    }

    [HttpPost("import/confirm")]
    [ValidateSessionCsrf]
    public async Task<IActionResult> Confirm(ConfirmImport request, CancellationToken ct)
    {
        var order = await service.ConfirmAsync(request.Token, UserId, UserName, ct);
        return CreatedAtAction(nameof(Detail), new { id = order.PurchaseOrderId }, order);
    }
}
