using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Procurement.BLL.PurchaseOrders;
using Procurement.Models.PurchaseOrders;
using Procurement.Models.Receiving;

namespace Procurement.Tests;

internal sealed class MemoryOrders : IPurchaseOrderRepository
{
    private long _version;
    private string Version() => Convert.ToBase64String(BitConverter.GetBytes(++_version));
    public List<StoredPurchaseOrder> Orders { get; } = [];
    public List<string> Actions { get; } = [];
    public Task<StoredPurchaseOrder[]> ListAsync(CancellationToken ct) => Task.FromResult(Orders.ToArray());
    public Task<StoredPurchaseOrder?> FindAsync(long id, CancellationToken ct) => Task.FromResult(Orders.SingleOrDefault(order => order.PurchaseOrderId == id));
    public Task<StoredPurchaseOrder> CreateAsync(CreatePurchaseOrder request, int userId, string name, string action, CancellationToken ct, string? importKey = null)
    {
        var id = Orders.Count + 1;
        var order = new StoredPurchaseOrder(id, "PO-TEST-" + id, request.SupplierId, request.WarehouseId, PurchaseOrderService.Today(),
            request.ExpectedArrivalDate, "DRAFT", DateTime.UtcNow, userId, request.Note,
            request.Lines.Select((line, index) => new StoredLine(index + 1, line.ProductId, line.Quantity)).ToArray(), Version());
        Orders.Add(order);
        Actions.Add(action);
        return Task.FromResult(order);
    }

    public Task<StoredPurchaseOrder> UpdateAsync(StoredPurchaseOrder expected, CreatePurchaseOrder request, int userId, string name, CancellationToken ct)
    {
        var current = Orders.Single(order => order.PurchaseOrderId == expected.PurchaseOrderId);
        if (current.RowVersion != expected.RowVersion) throw new PurchaseOrderConflictException("Stale");
        var next = current with { SupplierId = request.SupplierId, WarehouseId = request.WarehouseId,
            ExpectedArrivalDate = request.ExpectedArrivalDate, Note = request.Note, RowVersion = Version(),
            Lines = request.Lines.Select((line, index) => new StoredLine(index + 1, line.ProductId, line.Quantity)).ToArray() };
        Orders[Orders.IndexOf(current)] = next; Actions.Add("UPDATE_PO");
        return Task.FromResult(next);
    }

    public Task<StoredPurchaseOrder> TransitionAsync(StoredPurchaseOrder expected, string status, string? reason,
        IReadOnlyDictionary<long, int> closed, int userId, string name, CancellationToken ct)
    {
        var current = Orders.Single(order => order.PurchaseOrderId == expected.PurchaseOrderId);
        if (current.RowVersion != expected.RowVersion) throw new PurchaseOrderConflictException("Stale");
        var next = current with { Status = status, RowVersion = Version(), ConfirmedAt = status == "CONFIRMED" ? DateTime.UtcNow : current.ConfirmedAt,
            ClosedAt = status is "CLOSED" or "CANCELLED" ? DateTime.UtcNow : current.ClosedAt, CloseReason = reason,
            Lines = current.Lines.Select(line => line with { ClosedQuantity = closed.GetValueOrDefault(line.PoLineId) }).ToArray() };
        Orders[Orders.IndexOf(current)] = next; Actions.Add(status switch { "CONFIRMED" => "CONFIRM_PO", "CANCELLED" => "CANCEL_PO", _ => "CLOSE_PO" });
        return Task.FromResult(next);
    }
}
