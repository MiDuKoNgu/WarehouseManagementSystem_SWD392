using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procurement.BLL.PurchaseOrders;
using Procurement.DAL.Receiving;
using Procurement.Models.PurchaseOrders;
using Procurement.Models.Receiving;
using WMS.ServiceDefaults;

namespace Procurement.API.Controllers;

[ApiController, Route("api/receiving-orders")]
[Authorize(Roles = "WAREHOUSE_STAFF")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReceivingOrdersController(IPurchaseOrderRepository orders, IPurchaseOrderLookup lookup,
    IHttpClientFactory clients, IConfiguration configuration) : ControllerBase
{
    private int? WarehouseId => int.TryParse(User.FindFirstValue("warehouse_id"), out var value) && value > 0 ? value : null;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (WarehouseId is not { } warehouse) return Forbid();
        var options = await lookup.GetOptionsAsync(ct);
        return Ok((await orders.ListAsync(ct))
            .Where(order => order.WarehouseId == warehouse && order.Status.Equals("CONFIRMED", StringComparison.OrdinalIgnoreCase))
            .Select(order => Map(order, options)).ToArray());
    }

    [HttpGet("{id:long:min(1)}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
    {
        if (WarehouseId is not { } warehouse) return Forbid();
        var order = await orders.FindAsync(id, ct);
        if (order is null || order.WarehouseId != warehouse) return NotFound(new { message = "Không tìm thấy PO trong kho được phân công." });
        return Ok(Map(order, await lookup.GetOptionsAsync(ct)));
    }

    [HttpPost("{id:long:min(1)}/reserve"), ValidateSessionCsrf]
    public async Task<IActionResult> Reserve(long id, ReceivingReservationRequest request,
        [FromServices] ReceivingReservationRepository reservations, CancellationToken ct)
    {
        if (WarehouseId is not { } warehouse) return Forbid();
        await reservations.ReserveAsync(id, request.Token, warehouse,
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);
        return NoContent();
    }

    [HttpPost("{id:long:min(1)}/release"), ValidateSessionCsrf]
    public async Task<IActionResult> Release(long id, ReceivingReservationRequest request,
        [FromServices] ReceivingReservationRepository reservations, CancellationToken ct)
    {
        if (WarehouseId is not { } warehouse) return Forbid();
        if (request.Token == Guid.Empty) return BadRequest(new { message = "Mã yêu cầu không hợp lệ." });
        using var proofRequest = new HttpRequestMessage(HttpMethod.Get,
            new Uri(new Uri(configuration["Services:Inventory"] ?? "http://localhost:5159"), $"/api/inventory/receiving-reservations/{request.Token:D}"));
        proofRequest.Headers.TryAddWithoutValidation("Cookie", Request.Headers.Cookie.ToString());
        using var response = await clients.CreateClient("purchase-order-lookups").SendAsync(proofRequest, ct);
        if (!response.IsSuccessStatusCode) throw new ServiceUnavailableException("Cannot verify receipt reservation.");
        var proof = await response.Content.ReadFromJsonAsync<ReceivingReservationStatus>(cancellationToken: ct);
        if (proof is not { Exists: true, Terminal: true } || proof.PurchaseOrderId != id || proof.WarehouseId != warehouse)
            throw new PurchaseOrderConflictException("Chỉ được giải phóng yêu cầu sau khi phiếu nhập đã hoàn tất hoặc đã hủy.");
        await reservations.ReleaseAsync(id, request.Token, warehouse, ct);
        return NoContent();
    }

    private static ReceivingOrder Map(StoredPurchaseOrder order, PurchaseOrderOptions options) => new(
        order.PurchaseOrderId, order.PoCode, order.SupplierId,
        options.Suppliers.FirstOrDefault(value => value.SupplierId == order.SupplierId)?.SupplierName ?? $"Nhà cung cấp #{order.SupplierId}",
        order.WarehouseId, options.Warehouses.FirstOrDefault(value => value.WarehouseId == order.WarehouseId)?.WarehouseName ?? $"Kho #{order.WarehouseId}",
        order.Status.ToUpperInvariant(), order.Lines.Select(line =>
        {
            var product = options.Products.FirstOrDefault(value => value.ProductId == line.ProductId);
            return new ReceivingOrderLine(line.PoLineId, line.ProductId, product?.ProductCode ?? $"#{line.ProductId}",
                product?.ProductName ?? $"Sản phẩm #{line.ProductId}", line.Quantity, line.ClosedQuantity);
        }).ToArray());
}
