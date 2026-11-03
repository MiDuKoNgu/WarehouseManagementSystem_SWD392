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

public static class ProcurementTests
{
    public static async Task<int> Main()
    {
        await LiveReadTests.Run();
        EfMappingTests.Run();
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "Procurement.API"))) root = root.Parent;
        if (root is null) throw new Exception("Cannot locate Procurement.API.");
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_PROCUREMENT_API", Path.Combine(root.FullName, "Procurement.API"));
        var store = new MemoryOrders();
        var lookup = new FakeLookup();
        await using var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPurchaseOrderRepository>();
                services.RemoveAll<IPurchaseOrderLookup>();
                services.AddSingleton<IPurchaseOrderRepository>(store);
                services.AddSingleton<IPurchaseOrderLookup>(lookup);
                services.AddHttpClient("identity-session").ConfigurePrimaryHttpMessageHandler(() => new IdentityStub());
            });
        });
        using var anonymous = Client(factory, null);
        await Expect(await anonymous.GetAsync("/api/purchase-orders"), 401, "anonymous blocked");
        using var admin = Client(factory, "admin");
        await Expect(await admin.GetAsync("/api/purchase-orders/options"), 403, "admin cannot create PO");
        using var keeper = Client(factory, "warehouse");
        await Expect(await keeper.GetAsync("/api/purchase-orders"), 403, "warehouse staff blocked");
        await Expect(await anonymous.GetAsync("/api/receiving-orders"), 401, "receiving order reads require session");
        await Expect(await admin.GetAsync("/api/receiving-orders"), 403, "admin cannot read receiving order feed");
        using var unassignedKeeper = Client(factory, "warehouse-unassigned");
        await Expect(await unassignedKeeper.GetAsync("/api/receiving-orders"), 403, "warehouse assignment required for receiving");
        using var buyer = Client(factory, "buyer");
        await Expect(await buyer.GetAsync("/api/purchase-orders/options"), 200, "buyer options");

        var request = new CreatePurchaseOrder(1, 1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)), "Sample", [new(1, 3)]);
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders", request), 400, "CSRF required");
        buyer.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "valid");
        using var badCsrf = Client(factory, "buyer");
        badCsrf.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "invalid");
        await Expect(await badCsrf.PostAsJsonAsync("/api/purchase-orders", request), 400, "invalid CSRF rejected by Identity");
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders", request with { Lines = [new(1, 0)] }), 400, "quantity zero");
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders", request with { Lines = [new(1, 1), new(1, 2)] }), 400, "duplicate lines");
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders", request with { Lines = [new(2, 1)] }), 400, "supplier association");
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders", request with { WarehouseId = 999 }), 400, "unknown warehouse");
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders", request with { SupplierId = 999 }), 400, "unknown supplier");
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders", request with { Lines = [] }), 400, "empty PO");
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders", request with { ExpectedArrivalDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)) }), 400, "past delivery date");
        Check(store.Orders.Count == 0, "validation never writes");
        var createdResponse = await buyer.PostAsJsonAsync("/api/purchase-orders", request);
        await Expect(createdResponse, 201, "manual draft saved");
        var created = await createdResponse.Content.ReadFromJsonAsync<PurchaseOrderDetail>();
        Check(created is { Status: "DRAFT", CreatedByUserId: 22, TotalQuantity: 3 }, "creator comes from session and status DRAFT");
        Check(store.Actions.Single() == "CREATE_PO", "manual audit action");
        await Expect(await buyer.GetAsync($"/api/purchase-orders/{created!.PurchaseOrderId}"), 200, "detail");
        var listed = await buyer.GetFromJsonAsync<PurchaseOrderSummary[]>("/api/purchase-orders");
        Check(listed is { Length: 1 } && listed[0].PurchaseOrderId == created.PurchaseOrderId && listed[0].TotalQuantity == 3, "list persisted draft summary");
        await Expect(await buyer.GetAsync("/api/purchase-orders/9999"), 404, "missing detail");

        var template = await buyer.GetByteArrayAsync("/api/purchase-orders/import/template");
        Check(template.Length > 100, "template download");
        var invalid = Workbook(template, "NO-SUCH-CODE", 0);
        var invalidPreview = await Preview(buyer, invalid);
        Check(!invalidPreview.Valid && invalidPreview.Token is null && invalidPreview.Rows[0].Errors.Count > 0, "row errors visible");
        Check(store.Orders.Count == 1, "preview does not write");
        var bytes = Workbook(template, "SKU-1", 5);
        var preview = await Preview(buyer, bytes);
        Check(preview.Valid && preview.Token is not null, "valid preview token");
        Check(store.Orders.Count == 1, "valid preview also does not write");
        using var otherBuyer = Client(factory, "buyer2");
        otherBuyer.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "valid");
        await Expect(await otherBuyer.PostAsJsonAsync("/api/purchase-orders/import/confirm", new { preview.Token }), 409, "token bound to uploader");
        lookup.Active = false;
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders/import/confirm", new { preview.Token }), 400, "confirm revalidates master data");
        lookup.Active = true;
        var confirmations = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => buyer.PostAsJsonAsync("/api/purchase-orders/import/confirm", new { preview.Token })));
        foreach (var result in confirmations) await Expect(result, 201, "concurrent confirm succeeds idempotently");
        Check(store.Orders.Count == 2 && store.Actions.Count(action => action == "IMPORT_PO_EXCEL") == 1, "one imported PO and one audit only");
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders/import/confirm", new { token = "unknown" }), 409, "unknown token");
        using var badFile = new MultipartFormDataContent();
        badFile.Add(new ByteArrayContent([1, 2, 3]), "file", "bad.csv");
        await Expect(await buyer.PostAsync("/api/purchase-orders/import/preview", badFile), 400, "wrong file format");

        var first = (await buyer.GetFromJsonAsync<PurchaseOrderDetail>("/api/purchase-orders/1"))!;
        await Expect(await admin.PostAsJsonAsync("/api/purchase-orders/1/confirm", new { first.RowVersion }), 403, "admin cannot confirm PO");
        await Expect(await keeper.PostAsJsonAsync("/api/purchase-orders/1/cancel", new { first.RowVersion, reason = "Test" }), 403, "warehouse cannot cancel PO");
        await Expect(await badCsrf.PostAsJsonAsync("/api/purchase-orders/1/confirm", new { first.RowVersion }), 400, "transition CSRF required");
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders/1/confirm", new { rowVersion = "bad" }), 400, "invalid row version");
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders/1/confirm", new { rowVersion = Convert.ToBase64String(new byte[8]) }), 409, "stale row version");
        var edit = new UpdatePurchaseOrder(1, 1, null, "Edited", [new(1, 7)], first.RowVersion);
        lookup.HasInbound = true;
        await Expect(await buyer.PutAsJsonAsync("/api/purchase-orders/1", edit), 409, "draft edit blocked if inbound already exists");
        lookup.HasInbound = false;
        var edited = (await (await buyer.PutAsJsonAsync("/api/purchase-orders/1", edit)).Content.ReadFromJsonAsync<PurchaseOrderDetail>())!;
        Check(edited.TotalQuantity == 7 && edited.RowVersion != first.RowVersion, "draft edit persists and changes version");
        await Expect(await buyer.PutAsJsonAsync("/api/purchase-orders/1", edit), 409, "stale draft edit blocked");
        lookup.Active = false;
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders/1/confirm", new { edited.RowVersion }), 400, "confirm revalidates catalog");
        lookup.Active = true;
        var confirmResponse = await buyer.PostAsJsonAsync("/api/purchase-orders/1/confirm", new { edited.RowVersion });
        await Expect(confirmResponse, 200, "confirm draft");
        var confirmed = (await confirmResponse.Content.ReadFromJsonAsync<PurchaseOrderDetail>())!;
        Check(confirmed.Status == "CONFIRMED" && confirmed.ConfirmedAt is not null, "confirmation state and time");
        var foreignWarehouseOrder = store.Orders[0] with { PurchaseOrderId = 999, WarehouseId = 2 };
        store.Orders.Add(foreignWarehouseOrder);
        var eligible = (await keeper.GetFromJsonAsync<ReceivingOrder[]>("/api/receiving-orders"))!;
        Check(eligible.Length == 1 && eligible[0].PurchaseOrderId == confirmed.PurchaseOrderId
            && eligible[0].Lines[0].OrderedQuantity == 7, "receiving feed includes only confirmed assigned-warehouse PO and immutable line quantities");
        await Expect(await keeper.GetAsync("/api/receiving-orders/999"), 404, "other warehouse PO detail hidden");
        await Expect(await keeper.GetAsync($"/api/receiving-orders/{confirmed.PurchaseOrderId}"), 200, "assigned warehouse PO detail readable");
        store.Orders.Remove(foreignWarehouseOrder);
        await Expect(await buyer.PutAsJsonAsync("/api/purchase-orders/1", edit with { RowVersion = confirmed.RowVersion }), 409, "confirmed PO cannot edit");
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders/1/confirm", new { confirmed.RowVersion }), 409, "no repeated confirmation audit");
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders/1/close", new { confirmed.RowVersion, reason = " " }), 400, "closing reason required");
        lookup.HasInbound = true; lookup.OpenCount = 1;
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders/1/cancel", new { confirmed.RowVersion, reason = "Cancel" }), 409, "any inbound prevents cancellation");
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders/1/close", new { confirmed.RowVersion, reason = "Close" }), 409, "open inbound prevents close");
        lookup.OpenCount = 0; lookup.InvalidReceipts = true;
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders/1/close", new { confirmed.RowVersion, reason = "Close" }), 400, "corrupt duplicate/missing serial receipts rejected");
        lookup.InvalidReceipts = false; lookup.Received = [new(999, 1)];
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders/1/close", new { confirmed.RowVersion, reason = "Close" }), 400, "unknown received line rejected");
        lookup.Received = [new(confirmed.Lines[0].PoLineId, 8)];
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders/1/close", new { confirmed.RowVersion, reason = "Close" }), 400, "overreceived rejected");
        lookup.Received = [new(confirmed.Lines[0].PoLineId, -1)];
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders/1/close", new { confirmed.RowVersion, reason = "Close" }), 400, "negative received rejected");
        lookup.Received = [new(confirmed.Lines[0].PoLineId, 3)];
        var closeResponse = await buyer.PostAsJsonAsync("/api/purchase-orders/1/close", new { confirmed.RowVersion, reason = "Supplier cannot deliver remaining" });
        await Expect(closeResponse, 200, "short close");
        var closed = (await closeResponse.Content.ReadFromJsonAsync<PurchaseOrderDetail>())!;
        Check(closed is { Status: "CLOSED", ClosedAt: not null } && closed.Lines[0] is { ClosedQuantity: 4, ReceivedQuantity: 3, RemainingQuantity: 0 }, "short-close math and terminal metadata");
        lookup.HasInbound = false; lookup.Received = [];
        var second = (await buyer.GetFromJsonAsync<PurchaseOrderDetail>("/api/purchase-orders/2"))!;
        var cancelResponse = await buyer.PostAsJsonAsync("/api/purchase-orders/2/cancel", new { second.RowVersion, reason = "No longer needed" });
        await Expect(cancelResponse, 200, "cancel draft without inbound");
        var cancelled = (await cancelResponse.Content.ReadFromJsonAsync<PurchaseOrderDetail>())!;
        Check(cancelled.Status == "CANCELLED" && cancelled.CloseReason == "No longer needed", "cancel terminal reason");
        await Expect(await buyer.PostAsJsonAsync("/api/purchase-orders/2/confirm", new { cancelled.RowVersion }), 409, "cancelled PO cannot confirm");
        Check(store.Actions.Count(action => action == "CONFIRM_PO") == 1 && store.Actions.Contains("UPDATE_PO") && store.Actions.Contains("CLOSE_PO") && store.Actions.Contains("CANCEL_PO"), "transition audit actions");

        if (Environment.GetEnvironmentVariable("EWMS_PROCUREMENT_TEST_SERVER") is { Length: > 0 } server)
            await SqlTransactionTests.Run(server);
        else Console.WriteLine("SQL integration skipped: set EWMS_PROCUREMENT_TEST_SERVER to run against a disposable test database.");
        Console.WriteLine("PASS: PO authorization, CSRF, validation, draft/manual audit, list/detail, Excel template/preview, reference revalidation, user-scoped concurrent idempotent confirmation. No live business data modified.");
        return 0;
    }

    private static HttpClient Client(WebApplicationFactory<global::Program> factory, string? session)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (session is not null) client.DefaultRequestHeaders.Add("Cookie", "EWMS.Session=" + session);
        return client;
    }
    private static async Task<ImportPreview> Preview(HttpClient client, byte[] bytes)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes), "file", "purchase-order.xlsx");
        var result = await client.PostAsync("/api/purchase-orders/import/preview", content);
        await Expect(result, 200, "preview request");
        return (await result.Content.ReadFromJsonAsync<ImportPreview>())!;
    }
    private static byte[] Workbook(byte[] template, string code, int quantity)
    {
        using var input = new MemoryStream(template);
        using var workbook = new XLWorkbook(input);
        var sheet = workbook.Worksheet("PurchaseOrder");
        sheet.Cell(2, 1).Value = 1;
        sheet.Cell(2, 2).Value = 1;
        sheet.Cell(2, 5).Value = code;
        sheet.Cell(2, 6).Value = quantity;
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }
    private static async Task Expect(HttpResponseMessage response, int status, string name)
    {
        if ((int)response.StatusCode != status) throw new Exception($"FAIL {name}: expected {status}, got {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }
    internal static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL " + name); }
}
