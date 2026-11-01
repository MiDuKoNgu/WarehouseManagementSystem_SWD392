using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WMS.ServiceDefaults;

public sealed class ServiceUnavailableException(string message) : Exception(message);

public static class ServiceSecurity
{
    public static IServiceCollection AddEwmsServiceSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddHttpClient("identity-session", client =>
        {
            client.BaseAddress = new Uri(configuration["Services:Identity"] ?? "http://localhost:5140");
            client.Timeout = TimeSpan.FromSeconds(20);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
        services.AddAuthentication("IdentitySession").AddScheme<AuthenticationSchemeOptions, IdentitySessionHandler>("IdentitySession", _ => { });
        services.AddAuthorization();
        services.AddScoped<SessionCsrfFilter>();
        return services;
    }

    public static IApplicationBuilder UseEwmsServiceErrors(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        context.Response.Headers["Cache-Control"] = "no-store";
        try { await next(); }
        catch (Exception error) when (!context.Response.HasStarted &&
            (error is DbException or ServiceUnavailableException or HttpRequestException ||
             error is TaskCanceledException && !context.RequestAborted.IsCancellationRequested))
        {
            // Log type only, never SQL parameters, cookies or connection strings.
            context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("EWMS.Services")
                .LogError("Service request failed: {ErrorType}", error.GetType().Name);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { message = "Dịch vụ dữ liệu đang không khả dụng. Vui lòng thử lại sau." });
        }
    });
}

public sealed class IdentitySessionHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IHttpClientFactory clients) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.ContainsKey("EWMS.Session")) return AuthenticateResult.NoResult();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.TryAddWithoutValidation("Cookie", Request.Headers.Cookie.ToString());
        using var response = await clients.CreateClient("identity-session").SendAsync(request, Context.RequestAborted);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return AuthenticateResult.Fail("Session expired.");
        if (!response.IsSuccessStatusCode) throw new ServiceUnavailableException("Cannot validate session.");
        var user = await response.Content.ReadFromJsonAsync<SessionUser>(cancellationToken: Context.RequestAborted);
        if (user is null || user.UserId <= 0 || user.Role is not ("ADMIN" or "PURCHASING_STAFF" or "WAREHOUSE_STAFF"))
            return AuthenticateResult.Fail("Invalid session profile.");
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()), new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role), new("full_name", user.FullName)
        };
        if (user.WarehouseId is { } warehouseId) claims.Add(new("warehouse_id", warehouseId.ToString()));
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name));
    }

    private sealed record SessionUser(int UserId, string Username, string FullName, string Role, int? WarehouseId);
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ValidateSessionCsrfAttribute() : TypeFilterAttribute(typeof(SessionCsrfFilter));

public sealed class SessionCsrfFilter(IHttpClientFactory clients) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (!http.Request.Headers.TryGetValue("X-CSRF-TOKEN", out var token) || string.IsNullOrWhiteSpace(token))
        {
            context.Result = new BadRequestObjectResult(new { message = "Phiên biểu mẫu đã hết hạn. Vui lòng tải lại trang." });
            return;
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/validate-csrf");
        request.Headers.TryAddWithoutValidation("Cookie", http.Request.Headers.Cookie.ToString());
        request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token.ToString());
        using var response = await clients.CreateClient("identity-session").SendAsync(request, http.RequestAborted);
        if (response.IsSuccessStatusCode) { await next(); return; }
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            context.Result = new ObjectResult(new { message = "Phiên đăng nhập hoặc biểu mẫu không còn hợp lệ. Vui lòng tải lại trang." }) { StatusCode = (int)response.StatusCode };
        else throw new ServiceUnavailableException("Cannot validate CSRF.");
    }
}
