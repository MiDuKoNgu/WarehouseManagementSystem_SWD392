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

internal sealed class FakeLookup : IPurchaseOrderLookup
{
    public bool Active { get; set; } = true;
    public bool HasInbound { get; set; }
    public int OpenCount { get; set; }
    public bool InvalidReceipts { get; set; }
    public ReceivedLine[] Received { get; set; } = [];
    public Task<PurchaseOrderOptions> GetOptionsAsync(CancellationToken ct) => Task.FromResult(new PurchaseOrderOptions(
        [new(1, "Supplier")], [new(1, "Warehouse", "Ha Noi")], Active ? [new(1, "SKU-1", "Product", "Cai", [1]), new(2, "SKU-2", "Other", "Cai", [2])] : []));
    public Task<string?> GetCreatorNameAsync(int userId, CancellationToken ct) => Task.FromResult<string?>("Buyer Test");
    public Task<ReceivingSummary> GetReceivingSummaryAsync(long purchaseOrderId, CancellationToken ct) => Task.FromResult(new ReceivingSummary(purchaseOrderId, HasInbound, OpenCount, Received, InvalidReceipts));
}
