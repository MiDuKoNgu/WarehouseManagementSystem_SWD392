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

[ApiController, Route("api/inbounds"), Authorize(Roles = "WAREHOUSE_STAFF")]
public sealed class InboundsController(SqlInboundRepository receipts, ReceivingOrderClient orders, ReceiptPolicy policy, ReceiptQueries queries) : ControllerBase
{
    private ReceiptActor Actor => int.TryParse(User.FindFirstValue("warehouse_id"), out var warehouse) && warehouse > 0
        ? new(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), User.FindFirstValue("full_name") ?? User.Identity!.Name!, warehouse)
        : throw new ReceiptException(403, "Tài khoản chưa được phân công kho.");

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var actor = Actor;
        var headers = await receipts.ListAsync(actor, ct);
        var result = new List<ReceiptListItem>();
        var cache = new Dictionary<long, ReceiptOrder>();
        foreach (var h in headers)
        {
            if (!cache.TryGetValue(h.PurchaseOrderId, out var order)) cache[h.PurchaseOrderId] = order = await orders.FindAsync(h.PurchaseOrderId, ct);
            result.Add(new(h,order.PoCode,order.SupplierName,order.WarehouseName,order.Lines.Sum(line => (long)line.OrderedQuantity)));
        }
        return Ok(result);
    }
    [HttpGet("orders")]
    public async Task<IActionResult> Orders(CancellationToken ct)
    {
        var actor = Actor;
        var all = await orders.ListAsync(ct);
        var headers = policy.AllowMultipleDrafts ? [] : await receipts.ListAsync(actor, ct);
        var eligible = new List<ReceiptOrder>();
        foreach (var order in all.Where(order => order.WarehouseId == actor.WarehouseId))
            if (!headers.Any(h => h.PurchaseOrderId == order.PurchaseOrderId && h.Status == "DRAFT")
                && (await receipts.ProgressAsync(order, null, ct)).Any(l => l.RemainingQuantity > 0)) eligible.Add(order);
        return Ok(eligible);
    }
    [HttpGet("orders/{id:long:min(1)}")]
    public async Task<IActionResult> Order(long id, CancellationToken ct)
    {
        _ = Actor;
        var order = await orders.FindAsync(id, ct);
        return Ok(new { order, lines = await receipts.ProgressAsync(order, null, ct), policy });
    }
    [HttpGet("{id:long:min(1)}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct) => Ok(await Load(id, ct));

    [HttpPost, ValidateSessionCsrf]
    public async Task<IActionResult> Create(CreateReceipt request, CancellationToken ct)
    {
        var actor = Actor;
        if (request.Token == Guid.Empty || request.PurchaseOrderId <= 0 || request.Note?.Length > 1000)
            throw new ReceiptException(400, "Mã yêu cầu, PO hoặc ghi chú không hợp lệ.");
        var existing = await receipts.FindByTokenAsync(request.Token, actor, ct);
        if (existing is not null)
        {
            if (existing.PurchaseOrderId != request.PurchaseOrderId) throw new ReceiptException(409, "Mã yêu cầu đã dùng cho PO khác.");
            if (existing.Status == "CANCELLED") throw new ReceiptException(409, "Yêu cầu tạo phiếu đã hủy sau lỗi. Vui lòng tạo yêu cầu mới.");
            return Ok(await Load(existing.InboundId, ct));
        }
        var order = await orders.FindAsync(request.PurchaseOrderId, ct);
        if (!order.Status.Equals("CONFIRMED", StringComparison.OrdinalIgnoreCase)) throw new ReceiptException(409, "PO chưa được xác nhận.");
        if (!policy.AllowMultipleDrafts && (await receipts.ListAsync(actor, ct)).Any(h => h.PurchaseOrderId == order.PurchaseOrderId && h.Status == "DRAFT"))
            throw new ReceiptException(409, "PO đã có phiếu DRAFT. Vui lòng tiếp tục phiếu đó.");
        if (!(await receipts.ProgressAsync(order, null, ct)).Any(l => l.RemainingQuantity > 0)) throw new ReceiptException(409, "PO đã nhận đủ số lượng.");
        await orders.ReserveAsync(order.PurchaseOrderId, request.Token, ct);
        long id;
        try { id = await receipts.CreateAsync(order, request.Note, actor, ct, request.Token); }
        catch (ReceiptException)
        {
            // Fence late retries under the same Inventory lock before releasing Procurement.
            using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            if (await receipts.AbortCreationAsync(order,actor,request.Token,"Không thể tạo phiếu theo trạng thái/số lượng hiện tại.",recovery.Token))
                await orders.ReleaseAsync(order.PurchaseOrderId,request.Token,recovery.Token);
            throw;
        }
        return CreatedAtAction(nameof(Detail),new {id},await Load(id,ct));
    }
    [HttpPost("{id:long:min(1)}/items"), ValidateSessionCsrf]
    public async Task<IActionResult> Add(long id, SaveReceiptItem request, CancellationToken ct) => await Save(id,null,request,ct);
    [HttpPut("{id:long:min(1)}/items/{itemId:long:min(1)}"), ValidateSessionCsrf]
    public async Task<IActionResult> Update(long id,long itemId,SaveReceiptItem request,CancellationToken ct) => await Save(id,itemId,request,ct);
    private async Task<IActionResult> Save(long id,long? itemId,SaveReceiptItem request,CancellationToken ct)
    {
        var h = await Header(id,ct);
        await receipts.SaveItemAsync(id,itemId,request,await orders.FindAsync(h.PurchaseOrderId,ct),Actor,ct);
        return Ok(await Load(id,ct));
    }
    [HttpDelete("{id:long:min(1)}/items/{itemId:long:min(1)}"), ValidateSessionCsrf]
    public async Task<IActionResult> Remove(long id,long itemId,ReceiptTransition request,CancellationToken ct)
    { await receipts.RemoveItemAsync(id,itemId,request.RowVersion,Actor,ct); return Ok(await Load(id,ct)); }

    [HttpPost("{id:long:min(1)}/complete"), ValidateSessionCsrf]
    public async Task<IActionResult> Complete(long id,ReceiptTransition request,CancellationToken ct)
    {
        var h = await Header(id,ct);
        if (h.Status != "DONE") await receipts.CompleteAsync(id,request.RowVersion,await orders.FindAsync(h.PurchaseOrderId,ct),Actor,ct);
        // Retry after a lost response can release the durable reservation without receiving stock twice.
        await Release(id,h.PurchaseOrderId,ct);
        return Ok(await Load(id,ct));
    }
    [HttpPost("{id:long:min(1)}/cancel"), ValidateSessionCsrf]
    public async Task<IActionResult> Cancel(long id,ReceiptTransition request,CancellationToken ct)
    {
        var h = await Header(id,ct);
        if (h.Status != "CANCELLED") await receipts.CancelAsync(id,request.RowVersion,request.Reason ?? "",Actor,ct);
        await Release(id,h.PurchaseOrderId,ct);
        return Ok(await Load(id,ct));
    }
    private async Task Release(long id,long po,CancellationToken ct)
    { if (await queries.TokenAsync(id,Actor.WarehouseId,ct) is { } token) await orders.ReleaseAsync(po,token,ct); }
    private async Task<ReceiptHeader> Header(long id,CancellationToken ct) => await receipts.FindAsync(id,Actor,ct)
        ?? throw new ReceiptException(404,"Không tìm thấy phiếu nhập trong kho của bạn.");
    private async Task<ReceiptDetail> Load(long id,CancellationToken ct)
    {
        var h=await Header(id,ct); var order=await orders.FindAsync(h.PurchaseOrderId,ct);
        return new(h,order,await receipts.ProgressAsync(order,id,ct),await receipts.ItemsAsync(id,ct),policy);
    }
}

[ApiController, Route("api/inventory"), Authorize(Roles = "WAREHOUSE_STAFF")]
public sealed class ReceivingQueriesController(ReceiptQueries queries) : ControllerBase
{
    private int Warehouse => int.TryParse(User.FindFirstValue("warehouse_id"),out var id) && id>0 ? id : throw new ReceiptException(403,"Tài khoản chưa được phân công kho.");
    [HttpGet("receiving-reservations/{token:guid}")]
    public async Task<IActionResult> Reservation(Guid token,CancellationToken ct) => Ok(await queries.ReservationAsync(token,Warehouse,ct));
    [HttpGet("pending-putaway")]
    public async Task<IActionResult> Pending(CancellationToken ct) => Ok(await queries.PendingAsync(Warehouse,ct));
}
