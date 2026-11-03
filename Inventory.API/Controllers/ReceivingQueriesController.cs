using System.Security.Claims;
using Inventory.API.Clients;
using Inventory.BLL.Inbound;
using Inventory.DAL.Inbound;
using Inventory.DAL.Receiving;
using Inventory.Models.Inbound;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.ServiceDefaults;

namespace Inventory.API.Controllers;

[ApiController, Route("api/inventory"), Authorize(Roles = "WAREHOUSE_STAFF")]
public sealed class ReceivingQueriesController(ReceiptQueries queries) : ControllerBase
{
    private int Warehouse => int.TryParse(User.FindFirstValue("warehouse_id"),out var id) && id>0 ? id : throw new ReceiptException(403,"Tài khoản chưa được phân công kho.");
    [HttpGet("receiving-reservations/{token:guid}")]
    public async Task<IActionResult> Reservation(Guid token,CancellationToken ct) => Ok(await queries.ReservationAsync(token,Warehouse,ct));
    [HttpGet("pending-putaway")]
    public async Task<IActionResult> Pending(CancellationToken ct) => Ok(await queries.PendingAsync(Warehouse,ct));
}
