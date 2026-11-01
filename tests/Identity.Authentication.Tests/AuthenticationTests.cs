using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.BLL.Authentication;
using Identity.BLL.Security;
using Identity.Models.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;

namespace Identity.Authentication.Tests;

public static class AuthenticationTests
{
    public static async Task<int> Main()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "Identity.API"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Cannot locate Identity.API.");
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_IDENTITY_API", Path.Combine(root.FullName, "Identity.API"));

        var store = new FakeUsers();
        await using var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddSingleton<IUserRepository>(store);
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        });
        using var anonymous = Client(factory);
        await Status(await anonymous.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, "anonymous cannot read session");
        await Status(await anonymous.GetAsync("/api/workspaces/admin"), HttpStatusCode.Unauthorized, "anonymous cannot access admin");
        await Status(await anonymous.PostAsync("/api/auth/validate-csrf", null), HttpStatusCode.Unauthorized, "anonymous cannot validate mutation token");
        await Status(await anonymous.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = FakeUsers.Password }), HttpStatusCode.BadRequest, "login requires CSRF");
        await Login(factory, "missing", "wrong", HttpStatusCode.Unauthorized);
        await Login(factory, "admin", "wrong", HttpStatusCode.Unauthorized);
        await Login(factory, "disabled", FakeUsers.Password, HttpStatusCode.Forbidden);
        await Login(factory, "unassigned", FakeUsers.Password, HttpStatusCode.Forbidden);
        await Login(factory, "invalidWarehouse", FakeUsers.Password, HttpStatusCode.Forbidden);
        await Login(factory, "unknownRole", FakeUsers.Password, HttpStatusCode.Forbidden);

        foreach (var (username, endpoint, role) in new[] {
            ("admin", "admin", UserRoles.Admin), ("buyer", "purchasing", UserRoles.Purchasing), ("keeper", "warehouse", UserRoles.Warehouse) })
        {
            using var client = Client(factory);
            var response = await Post(client, "/api/auth/login", new { username, password = FakeUsers.Password, rememberMe = username == "buyer" });
            await Status(response, HttpStatusCode.OK, $"{role} login");
            var json = await response.Content.ReadAsStringAsync();
            Check(!json.Contains("passwordHash", StringComparison.OrdinalIgnoreCase), "response contains no password hash");
            var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("EWMS.Session="));
            Check(cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase), "session is HttpOnly");
            Check(cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase) == (username == "buyer"), "remember-me persistence");
            await Status(await client.GetAsync("/api/auth/me"), HttpStatusCode.OK, "session survives next request");
            await Status(await client.GetAsync("/api/auth/audit-users"), username == "admin" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, "audit employee lookup is admin-only");
            await Status(await client.GetAsync($"/api/workspaces/{endpoint}"), HttpStatusCode.OK, "own workspace allowed");
            await Status(await client.PostAsync("/api/auth/validate-csrf", null), HttpStatusCode.BadRequest, "cross-service validation rejects missing CSRF");
            await Status(await Post(client, "/api/auth/validate-csrf", null), HttpStatusCode.NoContent, "cross-service validation accepts identity-bound CSRF");
            await Status(await client.GetAsync("/api/auth/users/1/display-name"), username == "keeper" ? HttpStatusCode.Forbidden : HttpStatusCode.OK, "creator display-name access is role scoped");
            var other = endpoint == "admin" ? "purchasing" : "admin";
            await Status(await client.GetAsync($"/api/workspaces/{other}"), HttpStatusCode.Forbidden, "other role workspace blocked");
            store.FailUserLookup = true;
            try
            {
                await Status(await client.PostAsync("/api/auth/logout", null), HttpStatusCode.BadRequest, "logout requires CSRF even when SQL is unavailable");
                await Status(await Post(client, "/api/auth/logout", null), HttpStatusCode.NoContent, "logout succeeds without SQL, including CSRF issuance");
            }
            finally { store.FailUserLookup = false; }
            await Status(await client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, "logout clears session");
        }

        // Password/status/role/assignment edits change RowVersion and must invalidate existing cookies.
        using var revoked = Client(factory);
        // New factory resets rate limiter for the remaining cases.
        await using var second = factory.WithWebHostBuilder(_ => { });
        using var changed = Client(second);
        await Status(await Post(changed, "/api/auth/login", new { username = "keeper", password = FakeUsers.Password }), HttpStatusCode.OK, "warehouse login before unassignment");
        store.RemoveWarehouse();
        await Status(await changed.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, "unassignment revokes existing session");
        await using var limited = factory.WithWebHostBuilder(_ => { });
        using var attempts = Client(limited);
        for (var index = 0; index < 10; index++)
            await Status(await Post(attempts, "/api/auth/login", new { username = "missing", password = "wrong" }), HttpStatusCode.Unauthorized, "invalid login within limit");
        await Status(await Post(attempts, "/api/auth/login", new { username = "missing", password = "wrong" }), HttpStatusCode.TooManyRequests, "login rate limit");
        Console.WriteLine("PASS: authentication, all 3 roles, missing warehouse, inactive/unknown role, CSRF, persistence, authorization, logout, session revocation and rate limit. Fake repository only; no SQL data modified.");
        return 0;
    }

    private static HttpClient Client(WebApplicationFactory<global::Program> factory) => factory.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object? body)
    {
        var csrfResponse = await client.GetAsync("/api/auth/csrf");
        await Status(csrfResponse, HttpStatusCode.OK, "issue CSRF token");
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
    private static async Task Login(WebApplicationFactory<global::Program> factory, string username, string password, HttpStatusCode expected)
    {
        using var client = Client(factory);
        await Status(await Post(client, "/api/auth/login", new { username, password }), expected, username);
    }
    private static async Task Status(HttpResponseMessage response, HttpStatusCode expected, string test)
    {
        if (response.StatusCode != expected) throw new Exception($"FAIL {test}: expected {expected}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
    private static void Check(bool condition, string test) { if (!condition) throw new Exception("FAIL " + test); }
}

internal sealed class FakeUsers : IUserRepository
{
    public bool FailUserLookup { get; set; }
    public const string Password = "Test-only-Password-491!";
    private readonly List<AuthUser> _users;
    public FakeUsers()
    {
        var hash = new PasswordService().Hash(Password);
        _users = [
            new(1, "admin", "Admin Test", hash, UserRoles.Admin, null, "ACTIVE", [1]),
            new(2, "buyer", "Buyer Test", hash, UserRoles.Purchasing, null, "ACTIVE", [2]),
            new(3, "keeper", "Keeper Test", hash, UserRoles.Warehouse, 7, "ACTIVE", [3]),
            new(4, "disabled", "Disabled", hash, UserRoles.Admin, null, "INACTIVE", [4]),
            new(5, "unassigned", "Unassigned", hash, UserRoles.Warehouse, null, "ACTIVE", [5]),
            new(6, "invalidWarehouse", "Invalid", hash, UserRoles.Warehouse, 0, "ACTIVE", [6]),
            new(7, "unknownRole", "Unknown", hash, "OTHER", null, "ACTIVE", [7])
        ];
    }
    public void RemoveWarehouse() => _users[2] = _users[2] with { WarehouseId = null, RowVersion = [8] };
    public Task<AuthUser?> FindByUsernameAsync(string username, CancellationToken cancellationToken) => Task.FromResult(_users.Find(user => user.Username == username));
    public Task<AuthUser?> FindByIdAsync(int id, CancellationToken cancellationToken) => FailUserLookup
        ? throw new InvalidOperationException("Simulated SQL outage during session validation.")
        : Task.FromResult(_users.Find(user => user.UserId == id));
    public Task<UserDisplay[]> ListDisplayNamesAsync(CancellationToken cancellationToken) => Task.FromResult(_users.Select(user => new UserDisplay(user.UserId, user.Username, user.FullName)).ToArray());
    public Task UpdatePasswordHashAsync(AuthUser user, string hash, CancellationToken cancellationToken) => Task.CompletedTask;
}
