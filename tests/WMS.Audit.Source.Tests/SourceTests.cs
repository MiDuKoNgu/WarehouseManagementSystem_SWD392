extern alias InventoryService;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WMS.Audit;

namespace WMS.Audit.Source.Tests;

public static class SourceTests
{
    public static async Task<int> Main()
    {
        await QueryCompilationTests.Run();
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "Procurement.API"))) root = root.Parent;
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_PROCUREMENT_API", Path.Combine(root!.FullName, "Procurement.API"));
        await using var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Procurement"] = "Server=unused;Database=unused;Integrated Security=True;TrustServerCertificate=True" }));
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services => services.AddHttpClient("identity-session").ConfigurePrimaryHttpMessageHandler(() => new IdentityStub()));
        });
        foreach (var role in new[] { "", "buyer", "keeper" })
        {
            using var client = Client(factory, role);
            foreach (var path in new[] { "/api/audit-logs", "/api/audit-logs/actions", "/api/audit-logs/1" })
                await Status(await client.GetAsync(path), role == "" ? 401 : 403, "direct source audit role protection");
        }
        using var admin = Client(factory, "admin");
        await Status(await admin.GetAsync("/api/audit-logs?pageSize=0"), 400, "invalid source page size");
        await Status(await admin.GetAsync("/api/audit-logs?cursor=invalid"), 400, "invalid source cursor");
        await Status(await admin.PostAsJsonAsync("/api/audit-logs", new { }), 405, "no audit POST");
        await Status(await admin.DeleteAsync("/api/audit-logs/1"), 405, "no audit DELETE");
        await Status(await admin.PutAsJsonAsync("/api/audit-logs/1", new { }), 405, "no audit PUT");
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_INVENTORY_API", Path.Combine(root.FullName, "Inventory.API"));
        await using var inventory = new WebApplicationFactory<InventoryService::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Inventory"] = "Server=unused;Database=unused;Integrated Security=True;TrustServerCertificate=True" }));
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services => services.AddHttpClient("identity-session").ConfigurePrimaryHttpMessageHandler(() => new IdentityStub()));
        });
        foreach (var role in new[] { "", "buyer", "keeper" })
        {
            using var client = Client(inventory, role);
            foreach (var path in new[] { "/api/audit-logs", "/api/audit-logs/actions", "/api/audit-logs/1" })
                await Status(await client.GetAsync(path), role == "" ? 401 : 403, "inventory direct source protection");
        }
        using var inventoryAdmin = Client(inventory, "admin");
        await Status(await inventoryAdmin.GetAsync("/api/audit-logs?pageSize=0"), 400, "inventory admin query validation");
        await Status(await inventoryAdmin.DeleteAsync("/api/audit-logs/1"), 405, "inventory logs cannot be deleted");
        var sanitized = AuditValueRedactor.Sanitize("{\"passwordHash\":\"hidden-test\",\"lines\":[{\"quantity\":5,\"access_token\":\"secret-test\"}],\"importKey\":\"key-test\"}")!;
        Check(!sanitized.Contains("hidden-test") && !sanitized.Contains("secret-test") && !sanitized.Contains("key-test"), "source JSON redaction");
        Check(sanitized.Contains("quantity"), "business changes remain visible");
        Check(!AuditValueRedactor.Sanitize("broken password=hidden-test")!.Contains("hidden-test"), "malformed values hidden");
        if (Environment.GetEnvironmentVariable("EWMS_AUDIT_TEST_SERVER") is { Length: > 0 } server)
            await SqlReaderTests.Run(server);
        Console.WriteLine("PASS: source ADMIN-only read APIs, invalid filters/cursors, no mutation routes, sensitive JSON redaction.");
        return 0;
    }

    private static HttpClient Client<T>(WebApplicationFactory<T> factory, string role) where T : class
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (role.Length > 0) client.DefaultRequestHeaders.Add("Cookie", "EWMS.Session=" + role);
        return client;
    }
    public static void Check(bool condition, string message) { if (!condition) throw new Exception("FAIL: " + message); }
    private static async Task Status(HttpResponseMessage response, int expected, string message) =>
        Check((int)response.StatusCode == expected, $"{message}: expected {expected}, got {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

    private sealed class IdentityStub : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var cookie = string.Join(";", request.Headers.GetValues("Cookie"));
            var role = cookie.Contains("admin") ? "ADMIN" : cookie.Contains("buyer") ? "PURCHASING_STAFF" : "WAREHOUSE_STAFF";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { userId = 1, username = "source-test", fullName = "Source Test", role, warehouseId = 1 }) });
        }
    }
}
