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
