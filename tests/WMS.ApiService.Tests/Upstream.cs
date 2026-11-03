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
