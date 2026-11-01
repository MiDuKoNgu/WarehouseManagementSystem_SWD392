using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WMS.Audit;

namespace WMS.Audit.Gateway.Tests;

public static class AuditGatewayTests
{
    public static async Task<int> Main()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "WMS.API"))) root = root.Parent;
        if (root is null) throw new Exception("Cannot locate WMS.API.");
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_WMS_API", Path.Combine(root.FullName, "WMS.API"));
        var upstream = new AuditUpstream();
        await using var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.AddHttpClient("identity-session").ConfigurePrimaryHttpMessageHandler(() => new IdentityStub());
                services.AddHttpClient("audit-sources").ConfigurePrimaryHttpMessageHandler(() => new DelegatingStub(upstream));
            });
        });
        using var anonymous = Client(factory, null);
        await Expect(await anonymous.GetAsync("/api/admin/audit-logs"), 401, "anonymous blocked");
        foreach (var role in new[] { "buyer", "warehouse" })
        {
            using var wrongRole = Client(factory, role);
            await Expect(await wrongRole.GetAsync("/api/admin/audit-logs"), 403, "staff list blocked");
            await Expect(await wrongRole.GetAsync("/api/admin/audit-logs/options"), 403, "staff employee lookup blocked");
            await Expect(await wrongRole.GetAsync("/api/admin/audit-logs/procurement/10"), 403, "staff detail blocked");
        }
        using var admin = Client(factory, "admin");
        await Expect(await admin.PostAsJsonAsync("/api/admin/audit-logs", new { }), 405, "no create endpoint");
        await Expect(await admin.DeleteAsync("/api/admin/audit-logs/procurement/10"), 405, "no delete endpoint");
        await Expect(await admin.PutAsJsonAsync("/api/admin/audit-logs/procurement/10", new { }), 405, "no edit endpoint");
        await Expect(await admin.GetAsync("/api/admin/audit-logs?pageSize=1000"), 400, "page bounded");
        await Expect(await admin.GetAsync("/api/admin/audit-logs?businessGroup=OTHER"), 400, "group validation");
        await Expect(await admin.GetAsync("/api/admin/audit-logs?cursor=not-valid-cursor"), 400, "cursor validation");
        await Expect(await admin.GetAsync("/api/admin/audit-logs?from=2026-11-02T00:00:00Z&to=2026-11-01T00:00:00Z"), 400, "date interval validation");

        var first = await admin.GetFromJsonAsync<AuditPage>("/api/admin/audit-logs?pageSize=2");
        Check(first is { Items.Length: 2, TotalCount: 6, HasMore: true, NextCursor: not null }, "first merged page and totals");
        Check(first!.Items[0].Source == "inventory" && first.Items[0].AuditLogId == 12
            && first.Items[1].Source == "inventory" && first.Items[1].AuditLogId == 11, "source and ID tie-break sorting");
        Check(first.Items[0].ActorName == "Current Buyer" && first.Items[1].ActorName == "Snapshot Buyer", "actor lookup fills only missing snapshots");
        var all = first.Items.ToList();
        var cursor = first.NextCursor;
        while (cursor is not null)
        {
            var next = await admin.GetFromJsonAsync<AuditPage>("/api/admin/audit-logs?pageSize=2&cursor=" + Uri.EscapeDataString(cursor));
            Check(next is not null && next.Items.Length <= 2, "next page bounded");
            all.AddRange(next!.Items);
            cursor = next.NextCursor;
        }
        Check(all.Count == 6 && all.Select(item => (item.Source, item.AuditLogId)).Distinct().Count() == 6, "no duplicate or skipped tied rows across sources");
        Check(all[2].Source == "procurement" && all[2].AuditLogId == 10, "same timestamp procurement continues after inventory cursor");

        upstream.Seen.Clear();
        var filter = "?userId=9&businessGroup=PO&action=UPDATE_PO&documentCode=PO-2026%2F01&from=2026-11-01T00%3A00%3A00%2B07%3A00&to=2026-11-02T00%3A00%3A00%2B07%3A00&pageSize=5";
        await Expect(await admin.GetAsync("/api/admin/audit-logs" + filter), 200, "filtered list");
        foreach (var request in upstream.Seen.Where(uri => uri.AbsolutePath == "/api/audit-logs"))
        {
            var query = QueryHelpers.ParseQuery(request.Query);
            Check(query["userId"] == "9" && query["businessGroup"] == "PO" && query["action"] == "UPDATE_PO" && query["documentCode"] == "PO-2026/01", "filters forwarded unchanged");
            Check(DateTimeOffset.Parse(query["from"]!) == DateTimeOffset.Parse("2026-10-31T17:00:00Z"), "date converted to UTC without changing instant");
        }
        var options = await admin.GetFromJsonAsync<AuditOptions>("/api/admin/audit-logs/options");
        Check(options is { Employees.Length: 1 } && options.Actions.Contains("CUSTOM_ACTION") && options.Actions.Contains("CONFIRM_PUTAWAY"), "options combine employees and actual/known actions");

        var detailResponse = await admin.GetAsync("/api/admin/audit-logs/procurement/10");
        await Expect(detailResponse, 200, "detail read");
        var detail = await detailResponse.Content.ReadFromJsonAsync<AuditDetail>();
        Check(detail is not null && detail.Item.ActorName == "Current Buyer", "detail actor enriched");
        Check(!detail!.NewValues!.Contains("supersecret") && !detail.NewValues.Contains("private-import")
            && !detail.NewValues.Contains("nested-secret") && detail.NewValues.Contains("Draft"), "sensitive nested values redacted while business data retained");
        await Expect(await admin.GetAsync("/api/admin/audit-logs/unknown/10"), 404, "unknown source");
        await Expect(await admin.GetAsync("/api/admin/audit-logs/inventory/999"), 404, "missing detail");
        Check(!WMS.API.Audit.AuditGateway.Redact("password=supersecret malformed")!.Contains("supersecret"), "malformed payload hidden");
        upstream.FailInventory = true;
        await Expect(await admin.GetAsync("/api/admin/audit-logs"), 503, "unavailable source does not produce misleading partial history");
        Check(upstream.CookiesForwarded, "cookie forwarded to all service requests");
        Console.WriteLine("PASS: admin-only read APIs, no mutation routes, filter validation/forwarding, bounded cross-service cursor merge with timestamp ties, totals, employee names, detail redaction, 404 and upstream failure. Fake HTTP upstreams; no SQL data modified.");
        return 0;
    }

    private static HttpClient Client(WebApplicationFactory<global::Program> factory, string? session)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (session is not null) client.DefaultRequestHeaders.Add("Cookie", "EWMS.Session=" + session);
        return client;
    }
    private static async Task Expect(HttpResponseMessage response, int status, string test)
    {
        if ((int)response.StatusCode != status) throw new Exception($"FAIL {test}: expected {status}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
    internal static void Check(bool condition, string test) { if (!condition) throw new Exception("FAIL " + test); }
}

internal sealed class IdentityStub : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cookie = request.Headers.TryGetValues("Cookie", out var values) ? values.Single() : "";
        var role = cookie.Contains("=admin") ? "ADMIN" : cookie.Contains("=warehouse") ? "WAREHOUSE_STAFF" : "PURCHASING_STAFF";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { userId = 1, username = "admin", fullName = "Admin", role, warehouseId = (int?)null }) });
    }
}

internal sealed class DelegatingStub(AuditUpstream upstream) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => upstream.SendAsync(request);
}

internal sealed class AuditUpstream
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-11-01T04:00:00Z");
    private static AuditItem Item(string source, long id, int seconds = 0, string? actor = null) => new(source, id, 9, actor,
        source == "inventory" ? "INBOUND" : "PO", "CREATE_PO", source == "inventory" ? "Inbound" : "PurchaseOrder", "5", "PO-TEST", "SUCCESS", Time.AddSeconds(seconds));
    private readonly AuditItem[] _items = [Item("inventory", 12), Item("inventory", 11, actor: "Snapshot Buyer"), Item("procurement", 10), Item("procurement", 9), Item("inventory", 8, -1), Item("procurement", 7, -2)];
    public List<Uri> Seen { get; } = [];
    public bool FailInventory { get; set; }
    public bool CookiesForwarded { get; private set; } = true;

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        var uri = request.RequestUri!;
        lock (Seen) Seen.Add(uri);
        if (!request.Headers.TryGetValues("Cookie", out var cookies) || !cookies.Contains("EWMS.Session=admin")) CookiesForwarded = false;
        var source = uri.Port == 5159 ? "inventory" : "procurement";
        if (uri.Port == 5140) return Reply(new AuditEmployee[] { new(9, "buyer", "Current Buyer") });
        if (source == "inventory" && FailInventory) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        if (uri.AbsolutePath.EndsWith("/actions")) return Reply(new[] { "CUSTOM_ACTION", "CREATE_PO" });
        if (uri.AbsolutePath == "/api/audit-logs")
        {
            var query = QueryHelpers.ParseQuery(uri.Query);
            var size = int.Parse(query["pageSize"]!);
            var selected = _items.Where(item => item.Source == source);
            if (query.TryGetValue("cursor", out var token) && !string.IsNullOrWhiteSpace(token))
            {
                var cursor = AuditCursor.Decode(token!)!;
                selected = selected.Where(item => item.CreatedAt < cursor.CreatedAt || item.CreatedAt == cursor.CreatedAt
                    && (StringComparer.Ordinal.Compare(item.Source, cursor.Source) > 0 || item.Source == cursor.Source && item.AuditLogId < cursor.AuditLogId));
            }
            var ordered = selected.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.AuditLogId).ToArray();
            var rows = ordered.Take(size).ToArray();
            return Reply(new AuditPage(rows, _items.Count(item => item.Source == source), ordered.Length > rows.Length, null, size));
        }
        if (uri.AbsolutePath.EndsWith("/999")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        return Reply(new AuditDetail(Item(source, 10), null,
            "{\"status\":\"Draft\",\"passwordHash\":\"supersecret\",\"importKey\":\"private-import\",\"nested\":[{\"refresh_token\":\"nested-secret\"}]}"));
    }
    private static Task<HttpResponseMessage> Reply<T>(T value) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(value) });
}
