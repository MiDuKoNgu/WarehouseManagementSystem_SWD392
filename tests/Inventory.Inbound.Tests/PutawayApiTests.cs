using System.Net;
using System.Net.Http.Json;
using Inventory.BLL.Putaway;
using Inventory.Models.Putaway;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Inventory.Inbound.Tests;

internal static partial class PutawayApiTests
{
    public static async Task Run()
    {
        var fake = new Repository();
        await using var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                { ["ConnectionStrings:Inventory"] = "Server=unused;Database=unused;Integrated Security=true;Connect Timeout=1" }));
            builder.ConfigureLogging(log => log.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IPutawayRepository>(_ => fake);
                services.AddHttpClient("identity-session").ConfigurePrimaryHttpMessageHandler(() => new Identity());
            });
        });
        using var anonymous = factory.CreateClient();
        await Status(await anonymous.GetAsync("/api/putaway/pending"), 401, "anonymous pending");
        var request = new ConfirmPutaway(1, 3, Convert.ToBase64String(new byte[8]));
        foreach (var role in new[] { "admin", "buyer", "unassigned" })
        {
            using var denied = Client(factory, role);
            foreach (var path in new[] { "/api/putaway/pending", "/api/putaway/1", "/api/putaway/serials/TEST", "/api/putaway/1/suggestions" })
                await Status(await denied.GetAsync(path), 403, role + " " + path);
            denied.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "valid");
            await Status(await denied.PostAsJsonAsync("/api/putaway", request), 403, role + " write");
        }
        using var staff = Client(factory, "warehouse");
        await Status(await staff.PostAsJsonAsync("/api/putaway", request), 400, "missing CSRF");
        staff.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "invalid");
        await Status(await staff.PostAsJsonAsync("/api/putaway", request), 400, "invalid CSRF");
        staff.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); staff.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "valid");
        foreach (var query in new[] { "page=0", "page=1000001", "pageSize=0", "pageSize=101", "search=" + new string('a', 101) })
            await Status(await staff.GetAsync("/api/putaway/pending?" + query), 400, "invalid page/search");
        await Status(await staff.PostAsJsonAsync("/api/putaway", request with { RowVersion = "bad" }), 400, "invalid rowversion");
        await Status(await staff.PostAsJsonAsync("/api/putaway", request with { TargetLocationId = 0 }), 400, "invalid target");
        await Status(await staff.GetAsync("/api/putaway/pending?search=TV&page=2&pageSize=10"), 200, "pending query");
        InboundTests.Check(fake.LastActor is { WarehouseId: 1, UserId: 22 } && fake.LastQuery == ("TV", 2, 10), "Putaway query claims and pagination forwarded");
        var unusual = "ABC/12?%2F+#";
        await Status(await staff.GetAsync("/api/putaway/serials?serialNumber=" + Uri.EscapeDataString(unusual)), 200, "serial query accepts manufacturer punctuation");
        InboundTests.Check(fake.LastSerial == unusual, "query scan preserves exact serial punctuation");
        await Status(await staff.PostAsJsonAsync("/api/putaway", new { serialItemId = 1, targetLocationId = 3, request.RowVersion, actorUserId = 999, warehouseId = 2 }), 200, "valid CSRF write");
        InboundTests.Check(fake.LastActor is { UserId: 22, WarehouseId: 1 } && fake.Writes == 1, "spoofed client actor and warehouse ignored");
        fake.Error = new(409, "ALREADY_LOCATED", "Serial đã được đặt tại Kho A → A → Ô 1.", 3, "Kho A → A → Ô 1");
        var conflict = await staff.PostAsJsonAsync("/api/putaway", request);
        await Status(conflict, 409, "business conflict");
        var json = await conflict.Content.ReadAsStringAsync();
        InboundTests.Check(json.Contains("ALREADY_LOCATED") && json.Contains("currentLocationPath"), "retry includes current location and error code");
        Console.WriteLine("PASS Putaway API: role/warehouse isolation, claims, CSRF, paging/input, punctuation scan, actor spoofing, 409 location contract.");
    }
    private static HttpClient Client(WebApplicationFactory<global::Program> factory, string role)
    { var client = factory.CreateClient(new() { AllowAutoRedirect = false }); client.DefaultRequestHeaders.Add("Cookie", "EWMS.Session=" + role); return client; }
    private static async Task Status(HttpResponseMessage response, int expected, string label)
        => InboundTests.Check((int)response.StatusCode == expected, $"Putaway {label}: expected {expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
}
