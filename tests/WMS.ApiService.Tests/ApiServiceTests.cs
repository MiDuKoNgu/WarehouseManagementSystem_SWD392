extern alias WmsApi;
extern alias IdentityApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Identity.BLL.Authentication;
using Identity.BLL.Security;
using Identity.Models.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace WMS.ApiService.Tests;

public static class ApiServiceTests
{
    private static readonly (string Prefix, string Service)[] Routes =
    [
        ("auth", "Identity"), ("workspaces", "Identity"),
        ("products", "Catalog"), ("catalog", "Catalog"),
        ("warehouses", "Warehouse"),
        ("purchase-orders", "Procurement"), ("receiving-orders", "Procurement"),
        ("inbounds", "Inventory"), ("inventory", "Inventory"), ("putaway", "Inventory")
    ];

    public static async Task<int> Main()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "WMS.API"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Cannot locate solution root.");
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_WMS_API", Path.Combine(root.FullName, "WMS.API"));
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_IDENTITY_API", Path.Combine(root.FullName, "Identity.API"));

        await TestTransport();
        await TestIdentity();
        Console.WriteLine("PASS: all 10 route families, exact/nested paths, methods/query/JSON, CSRF/cookies, multipart/binary files, independent clients, upstream statuses/redirect/failure, unknown routes; real Identity login/session/roles/logout/revocation through WMS with fake repository. No SQL connections or business data writes.");
        return 0;
    }

    private static async Task TestTransport()
    {
        var hosts = new Dictionary<string, Upstream>();
        try
        {
            foreach (var name in Routes.Select(route => route.Service).Distinct())
                hosts.Add(name, await Upstream.Start(name));
            await using var gateway = Gateway(hosts.ToDictionary(entry => entry.Key, entry => entry.Value.Address));
            using var client = gateway.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = false });

            foreach (var (prefix, service) in Routes)
            {
                foreach (var suffix in new[] { "", "/nested/27" })
                {
                    var response = await client.GetAsync($"/api/{prefix}{suffix}");
                    await Status(response, 200, $"route {prefix}{suffix}");
                    var echo = await response.Content.ReadFromJsonAsync<RequestEcho>();
                    Check(echo?.Service == service && echo.Path == $"/api/{prefix}{suffix}", $"{prefix}{suffix} reaches correct service without rewrite");
                }
            }

            const string serial = "SER/01%+ space &? tiếng Việt";
            var serialPath = "/api/putaway/serials?serialNumber=" + Uri.EscapeDataString(serial) + "&tag=one&tag=two";
            var serialEcho = await client.GetFromJsonAsync<RequestEcho>(serialPath);
            var query = QueryHelpers.ParseQuery(serialEcho!.Query);
            Check(query["serialNumber"] == serial && query["tag"].SequenceEqual(new[] { "one", "two" }), "encoded serial and repeated query parameters preserved");

            foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
            {
                const string body = "{\"rowVersion\":\"A+/=\",\"reason\":\"Kiểm tra\"}";
                using var request = new HttpRequestMessage(method, "/api/inbounds/4/items/6?mode=edit");
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                request.Headers.Add("Cookie", "EWMS.Session=client-a; EWMS.Csrf=csrf-a");
                request.Headers.Add("X-CSRF-TOKEN", "token-a");
                using var response = await client.SendAsync(request);
                await Status(response, 200, method + " request");
                var echo = await response.Content.ReadFromJsonAsync<RequestEcho>();
                Check(echo?.Method == method.Method && echo.Body == body && echo.Query == "?mode=edit", method + " body/query preserved");
                Check(echo!.Cookie == "EWMS.Session=client-a; EWMS.Csrf=csrf-a" && echo.Csrf == "token-a", method + " session and CSRF forwarded without WMS session lookup");
                Check(echo.ContentType?.StartsWith("application/json", StringComparison.Ordinal) == true, "JSON content type preserved");
            }

            byte[] bytes = [0x50, 0x4b, 0x03, 0x04, 0, 255, 128, 13, 10];
            using (var form = new MultipartFormDataContent("gateway-test-boundary"))
            {
                var file = new ByteArrayContent(bytes);
                file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                form.Add(file, "file", "purchase-order.xlsx");
                using var upload = new HttpRequestMessage(HttpMethod.Post, "/api/purchase-orders/import/preview") { Content = form };
                upload.Headers.Add("X-CSRF-TOKEN", "upload-token");
                using var response = await client.SendAsync(upload);
                await Status(response, 200, "multipart upload");
                var received = await response.Content.ReadFromJsonAsync<JsonElement>();
                Check(received.GetProperty("fileName").GetString() == "purchase-order.xlsx" && received.GetProperty("body").GetString() == Convert.ToBase64String(bytes), "multipart filename and binary bytes preserved");
                Check(received.GetProperty("csrf").GetString() == "upload-token", "multipart CSRF preserved");
            }

            using (var response = await client.GetAsync("/api/purchase-orders/import/template"))
            {
                await Status(response, 200, "binary download");
                Check((await response.Content.ReadAsByteArrayAsync()).SequenceEqual(bytes), "download bytes preserved");
                Check(response.Content.Headers.ContentType?.MediaType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "download type preserved");
                Check(response.Content.Headers.ContentDisposition?.FileName == "EWMS-PO-template.xlsx", "download filename preserved");
            }

            using var clientA = gateway.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
            using var clientB = gateway.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
            using (var cookies = await clientA.GetAsync("/api/auth/test-cookies"))
            {
                Check(cookies.Headers.GetValues("Set-Cookie").Count() == 2, "multiple Set-Cookie values remain separate");
            }
            var a = await clientA.GetFromJsonAsync<RequestEcho>("/api/auth/echo");
            var b = await clientB.GetFromJsonAsync<RequestEcho>("/api/auth/echo");
            Check(a!.Cookie.Contains("EWMS.Session=client-a") && a.Cookie.Contains("EWMS.Csrf=csrf-a"), "browser receives and reuses cookies");
            Check(string.IsNullOrEmpty(b!.Cookie), "gateway does not share another client's cookies");

            foreach (var status in new[] { 400, 401, 403, 404, 409, 429, 503 })
            {
                using var response = await client.GetAsync($"/api/auth/test-status/{status}");
                await Status(response, status, "upstream status retained");
                var error = await response.Content.ReadFromJsonAsync<JsonElement>();
                Check(error.GetProperty("message").GetString() == "upstream-" + status, "upstream error payload retained");
                if (status == 429) Check(response.Headers.RetryAfter?.Delta == TimeSpan.FromSeconds(60), "Retry-After retained");
            }
            using (var response = await client.GetAsync("/api/auth/test-redirect"))
            {
                await Status(response, 302, "redirect not followed by gateway");
                Check(response.Headers.Location?.OriginalString == "/login", "redirect location retained");
            }
            foreach (var path in new[] { "/api/unknown", "/api/products-other", "/api/audit-logs", "/api" })
                await Status(await client.GetAsync(path), 404, "unknown or ambiguous path stays local: " + path);

            var identityCalls = 0;
            await using (var unavailableIdentity = gateway.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddHttpClient("identity-session").ConfigurePrimaryHttpMessageHandler(() =>
                    new UnavailableIdentityHandler(() => Interlocked.Increment(ref identityCalls))))))
            {
                using var stale = unavailableIdentity.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = false });
                stale.DefaultRequestHeaders.Add("Cookie", "EWMS.Session=stale-session");
                foreach (var path in new[] { "/api/products-other", "/api/admin/unknown", "/api/admin/audit-logs/unknown", "/api/unknown" })
                    await Status(await stale.GetAsync(path), 404, "unknown API does not validate stale cookie: " + path);
                Check(identityCalls == 0, "unknown API never invokes unavailable session service");
                await Status(await stale.GetAsync("/api/admin/audit-logs"), 503, "real local audit route still validates its session");
                Check(identityCalls == 1, "local audit authentication remains active");
            }

            await hosts["Catalog"].Stop();
            using (var response = await client.GetAsync("/api/products"))
            {
                await Status(response, 502, "offline upstream returns gateway failure");
                var error = await response.Content.ReadFromJsonAsync<JsonElement>();
                Check(!string.IsNullOrWhiteSpace(error.GetProperty("message").GetString()), "gateway failure has usable JSON message");
                Check(!(await response.Content.ReadAsStringAsync()).Contains(hosts["Catalog"].Address), "gateway failure hides internal address");
            }
        }
        finally { foreach (var host in hosts.Values) await host.DisposeAsync(); }
    }

    private static async Task TestIdentity()
    {
        var users = new FakeUsers();
        await using var identity = new WebApplicationFactory<IdentityApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddSingleton<IUserRepository>(users);
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        });
        using var identityClient = identity.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = false });
        await using var bridge = await Upstream.Start("Identity", context => Bridge(context, identityClient));
        await using var gateway = Gateway(Routes.Select(route => route.Service).Distinct().ToDictionary(name => name, _ => bridge.Address));
        using var client = gateway.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });

        await Status(await client.GetAsync("/api/auth/me"), 401, "real Identity anonymous session through gateway");
        await Status(await client.PostAsJsonAsync("/api/auth/login", new { username = "buyer", password = FakeUsers.Password }), 400, "real Identity rejects login without CSRF through gateway");
        await Status(await PostWithCsrf(client, "/api/auth/login", new { username = "buyer", password = "wrong" }), 401, "real Identity wrong password through gateway");
        using var login = await PostWithCsrf(client, "/api/auth/login", new { username = "buyer", password = FakeUsers.Password, rememberMe = true });
        await Status(login, 200, "real Identity login through gateway");
        Check(login.Headers.GetValues("Set-Cookie").Any(value => value.StartsWith("EWMS.Session=") && value.Contains("httponly", StringComparison.OrdinalIgnoreCase) && value.Contains("expires=", StringComparison.OrdinalIgnoreCase)), "real session cookie attributes retained");
        var profile = await client.GetFromJsonAsync<UserProfile>("/api/auth/me");
        Check(profile?.Username == "buyer" && profile.Role == UserRoles.Purchasing, "real cookie round-trip through gateway");
        await Status(await client.GetAsync("/api/workspaces/purchasing"), 200, "real workspace authorized through gateway");
        await Status(await client.GetAsync("/api/workspaces/admin"), 403, "real workspace role denial through gateway");
        users.FailLookup = true;
        try
        {
            await Status(await client.PostAsync("/api/auth/logout", null), 400, "logout still requires CSRF during lookup outage");
            await Status(await PostWithCsrf(client, "/api/auth/logout", null), 204, "CSRF and logout bypass user lookup even through gateway");
        }
        finally { users.FailLookup = false; }
        await Status(await client.GetAsync("/api/auth/me"), 401, "real logout cookie deletion preserved");

        await Status(await PostWithCsrf(client, "/api/auth/login", new { username = "buyer", password = FakeUsers.Password }), 200, "login before revocation");
        users.ChangeVersion();
        await Status(await client.GetAsync("/api/auth/me"), 401, "real session revocation through gateway");

        await Status(await PostWithCsrf(client, "/api/auth/login", new { username = "buyer", password = FakeUsers.Password }), 200, "login before stale-session logout");
        users.ChangeVersion();
        await Status(await PostWithCsrf(client, "/api/auth/logout", null), 204, "stale session can obtain identity-bound CSRF and log out");
    }

    private static WebApplicationFactory<WmsApi::Program> Gateway(Dictionary<string, string> addresses) =>
        new WebApplicationFactory<WmsApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                addresses.Select(item => new KeyValuePair<string, string?>("Services:" + item.Key, item.Value))));
        });

    private static async Task<HttpResponseMessage> PostWithCsrf(HttpClient client, string path, object? body)
    {
        using var issued = await client.GetAsync("/api/auth/csrf");
        await Status(issued, 200, "real CSRF issuance through gateway");
        var csrf = await issued.Content.ReadFromJsonAsync<JsonElement>();
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    // Expose the real Identity TestServer over loopback so WMS uses its normal HTTP
    // transport. This adapter changes no auth behavior and stores no cookies.
    private static async Task Bridge(HttpContext context, HttpClient identity)
    {
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), context.Request.Path + context.Request.QueryString);
        using var body = new MemoryStream();
        await context.Request.Body.CopyToAsync(body, context.RequestAborted);
        if (body.Length > 0 || context.Request.ContentLength.HasValue) request.Content = new ByteArrayContent(body.ToArray());
        foreach (var header in context.Request.Headers)
        {
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) || header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) continue;
            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
        using var response = await identity.SendAsync(request, context.RequestAborted);
        context.Response.StatusCode = (int)response.StatusCode;
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            if (header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) continue;
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }
        await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }

    private static async Task Status(HttpResponseMessage response, int expected, string test)
    {
        if ((int)response.StatusCode != expected)
            throw new Exception($"FAIL {test}: expected {expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
    private static void Check(bool condition, string test) { if (!condition) throw new Exception("FAIL " + test); }
}

internal sealed record RequestEcho(string Service, string Method, string Path, string Query, string Cookie, string Csrf, string? ContentType, string Body);

internal sealed class UnavailableIdentityHandler(Action onCall) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        onCall();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}

internal sealed class Upstream(WebApplication app) : IAsyncDisposable
{
    public string Address => app.Urls.Single();
    public static async Task<Upstream> Start(string name, RequestDelegate? custom = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.Run(custom ?? (context => Reply(context, name)));
        await app.StartAsync();
        return new(app);
    }
    private static async Task Reply(HttpContext context, string name)
    {
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/api/auth/test-status/", StringComparison.Ordinal))
        {
            var status = int.Parse(path.Split('/')[^1]);
            context.Response.StatusCode = status;
            if (status == 429) context.Response.Headers.RetryAfter = "60";
            await context.Response.WriteAsJsonAsync(new { message = "upstream-" + status });
            return;
        }
        if (path == "/api/auth/test-redirect")
        {
            context.Response.StatusCode = 302;
            context.Response.Headers.Location = "/login";
            return;
        }
        if (path == "/api/auth/test-cookies")
        {
            context.Response.Headers.Append("Set-Cookie", "EWMS.Session=client-a; Path=/; HttpOnly; SameSite=Strict");
            context.Response.Headers.Append("Set-Cookie", "EWMS.Csrf=csrf-a; Path=/; HttpOnly; SameSite=Strict");
        }
        if (path == "/api/purchase-orders/import/template")
        {
            context.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            context.Response.Headers.ContentDisposition = "attachment; filename=EWMS-PO-template.xlsx";
            await context.Response.Body.WriteAsync(new byte[] { 0x50, 0x4b, 0x03, 0x04, 0, 255, 128, 13, 10 });
            return;
        }
        if (path == "/api/purchase-orders/import/preview")
        {
            var form = await context.Request.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? throw new Exception("Missing forwarded file");
            using var bytes = new MemoryStream();
            await file.CopyToAsync(bytes);
            await context.Response.WriteAsJsonAsync(new { fileName = file.FileName, body = Convert.ToBase64String(bytes.ToArray()), csrf = context.Request.Headers["X-CSRF-TOKEN"].ToString() });
            return;
        }
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
        await context.Response.WriteAsJsonAsync(new RequestEcho(name, context.Request.Method, path, context.Request.QueryString.ToString(),
            context.Request.Headers.Cookie.ToString(), context.Request.Headers["X-CSRF-TOKEN"].ToString(), context.Request.ContentType, await reader.ReadToEndAsync()));
    }
    public Task Stop() => app.StopAsync();
    public ValueTask DisposeAsync() => app.DisposeAsync();
}

internal sealed class FakeUsers : IUserRepository
{
    public const string Password = "Gateway-test-only-password-723!";
    private AuthUser buyer = new(7, "buyer", "Gateway Buyer", new PasswordService().Hash(Password), UserRoles.Purchasing, null, "ACTIVE", [1]);
    public bool FailLookup { get; set; }
    public void ChangeVersion() => buyer = buyer with { RowVersion = [(byte)(buyer.RowVersion[0] + 1)] };
    public Task<AuthUser?> FindByUsernameAsync(string username, CancellationToken cancellationToken) => Task.FromResult<AuthUser?>(username == buyer.Username ? buyer : null);
    public Task<AuthUser?> FindByIdAsync(int id, CancellationToken cancellationToken) => FailLookup
        ? throw new InvalidOperationException("Simulated unavailable user store")
        : Task.FromResult<AuthUser?>(id == buyer.UserId ? buyer : null);
    public Task<UserDisplay[]> ListDisplayNamesAsync(CancellationToken cancellationToken) => Task.FromResult(new[] { new UserDisplay(buyer.UserId, buyer.Username, buyer.FullName) });
    public Task UpdatePasswordHashAsync(AuthUser user, string hash, CancellationToken cancellationToken)
    { buyer = buyer with { PasswordHash = hash }; return Task.CompletedTask; }
}
