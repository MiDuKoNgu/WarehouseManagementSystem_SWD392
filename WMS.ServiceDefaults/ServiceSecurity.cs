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
