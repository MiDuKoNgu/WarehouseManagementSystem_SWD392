using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using WMS.Audit;
using WMS.ServiceDefaults;

namespace WMS.API.Audit;

public sealed class AuditGateway(IHttpClientFactory clients, IHttpContextAccessor context, IConfiguration configuration)
{
    private static readonly string[] Sources = ["inventory", "procurement"];
    private static readonly string[] KnownActions = ["CREATE_PO", "IMPORT_PO_EXCEL", "UPDATE_PO", "CONFIRM_PO", "CANCEL_PO", "CLOSE_PO",
        "CREATE_INBOUND", "UPDATE_INBOUND", "COMPLETE_INBOUND", "CONFIRM_PUTAWAY"];

    public async Task<AuditPage> ListAsync(AuditQuery query, CancellationToken ct)
    {
        query.Validate();
        var path = QueryHelpers.AddQueryString("/api/audit-logs", QueryValues(query));
        var sourceTasks = Sources.Select(source => GetAsync<AuditPage>(source, path, ct)).ToArray();
        var employeesTask = EmployeesAsync(ct);
        await Task.WhenAll(sourceTasks.Cast<Task>().Append(employeesTask));
        var pages = sourceTasks.Select(task => task.Result!).ToArray();
        var employees = employeesTask.Result.ToDictionary(employee => employee.UserId);
        var combined = pages.SelectMany(page => page.Items)
            .OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Source, StringComparer.Ordinal)
            .ThenByDescending(item => item.AuditLogId).ToArray();
        var items = combined.Take(query.PageSize).Select(item => FillActor(item, employees)).ToArray();
        var more = combined.Length > items.Length || pages.Any(page => page.HasMore);
        var cursor = more && items.Length > 0
            ? AuditCursor.Encode(new AuditCursor(items[^1].CreatedAt, items[^1].Source, items[^1].AuditLogId)) : null;
        return new(items, checked(pages.Sum(page => page.TotalCount)), more, cursor, query.PageSize);
    }

    public async Task<AuditDetail?> DetailAsync(string source, long id, CancellationToken ct)
    {
        if (!Sources.Contains(source, StringComparer.Ordinal) || id <= 0) return null;
        var detail = await GetAsync<AuditDetail>(source, $"/api/audit-logs/{id}", ct, allowNotFound: true);
        if (detail is null) return null;
        var employees = (await EmployeesAsync(ct)).ToDictionary(employee => employee.UserId);
        return new(FillActor(detail.Item, employees), Redact(detail.OldValues), Redact(detail.NewValues));
    }

    public async Task<AuditOptions> OptionsAsync(CancellationToken ct)
    {
        var employees = EmployeesAsync(ct);
        var actions = Sources.Select(source => GetAsync<string[]>(source, "/api/audit-logs/actions", ct)).ToArray();
        await Task.WhenAll(actions.Cast<Task>().Append(employees));
        return new(employees.Result, KnownActions.Concat(actions.SelectMany(task => task.Result!))
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private Task<AuditEmployee[]> EmployeesAsync(CancellationToken ct) =>
        RequiredAsync<AuditEmployee[]>("identity", "/api/auth/audit-users", ct);

    private async Task<T> RequiredAsync<T>(string source, string path, CancellationToken ct) where T : class =>
        await GetAsync<T>(source, path, ct) ?? throw new ServiceUnavailableException("Audit upstream returned no data.");

    private async Task<T?> GetAsync<T>(string source, string path, CancellationToken ct, bool allowNotFound = false) where T : class
    {
        var (name, fallback) = source switch
        {
            "identity" => ("Identity", "http://localhost:5140"),
            "inventory" => ("Inventory", "http://localhost:5159"),
            "procurement" => ("Procurement", "http://localhost:5136"),
            _ => throw new ServiceUnavailableException("Unsupported audit source.")
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(configuration[$"Services:{name}"] ?? fallback), path));
        request.Headers.TryAddWithoutValidation("Cookie", context.HttpContext?.Request.Headers.Cookie.ToString());
        using var response = await clients.CreateClient("audit-sources").SendAsync(request, ct);
        if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) throw new ServiceUnavailableException($"Audit source {source} is not available.");
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
                ?? throw new ServiceUnavailableException("Audit source returned an empty response.");
        }
        catch (JsonException) { throw new ServiceUnavailableException("Audit source returned invalid JSON."); }
    }

    private static Dictionary<string, string?> QueryValues(AuditQuery query) => new()
    {
        ["userId"] = query.UserId?.ToString(CultureInfo.InvariantCulture), ["businessGroup"] = query.BusinessGroup,
        ["action"] = query.Action, ["documentCode"] = query.DocumentCode,
        ["from"] = query.From?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        ["to"] = query.To?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture), ["cursor"] = query.Cursor
    };

    private static AuditItem FillActor(AuditItem item, Dictionary<int, AuditEmployee> employees) =>
        !string.IsNullOrWhiteSpace(item.ActorName) ? item
            : item with { ActorName = employees.TryGetValue(item.UserId, out var employee) ? employee.FullName : $"Nhân viên #{item.UserId}" };

    public static string? Redact(string? value) => AuditValueRedactor.Sanitize(value);
}
