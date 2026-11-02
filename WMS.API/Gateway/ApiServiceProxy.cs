using System.Net;
using Yarp.ReverseProxy.Forwarder;

namespace WMS.API.Gateway;

/// <summary>The public API entry point. Domain services still own authorization and business rules.</summary>
public sealed class ApiServiceProxy
{
    private readonly (PathString Prefix, string Destination)[] routes;
    private static readonly ForwarderRequestConfig RequestConfig = new()
    {
        ActivityTimeout = TimeSpan.FromSeconds(100)
    };

    public ApiServiceProxy(IConfiguration configuration)
    {
        var identity = Destination(configuration, "Identity", "http://localhost:5140");
        var catalog = Destination(configuration, "Catalog", "http://localhost:5254");
        var warehouse = Destination(configuration, "Warehouse", "http://localhost:5104");
        var procurement = Destination(configuration, "Procurement", "http://localhost:5136");
        var inventory = Destination(configuration, "Inventory", "http://localhost:5159");
        routes =
        [
            (new("/api/auth"), identity),
            (new("/api/workspaces"), identity),
            (new("/api/products"), catalog),
            (new("/api/catalog"), catalog),
            (new("/api/warehouses"), warehouse),
            (new("/api/purchase-orders"), procurement),
            (new("/api/receiving-orders"), procurement),
            (new("/api/inbounds"), inventory),
            (new("/api/inventory"), inventory),
            (new("/api/putaway"), inventory)
        ];
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next,
        IHttpForwarder forwarder, HttpMessageInvoker client)
    {
        foreach (var (prefix, destination) in routes)
        {
            // Segment matching also accepts the collection URL itself, but never products-other.
            if (!context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            var error = await forwarder.SendAsync(context, destination, client, RequestConfig, HttpTransformer.Default);
            if (error != ForwarderError.None && !context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
            {
                // YARP sets 502/504 for transport failures. Keep domain error responses untouched.
                if (context.Response.StatusCode < 500) context.Response.StatusCode = StatusCodes.Status502BadGateway;
                context.Response.ContentLength = null;
                await context.Response.WriteAsJsonAsync(new { message = "Không thể kết nối dịch vụ nghiệp vụ. Vui lòng thử lại sau." }, context.RequestAborted);
            }
            return;
        }

        // Routing ran first: only local controller endpoints (including method-not-allowed
        // endpoints) should enter WMS authentication. Unknown APIs must not depend on Identity.
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            && context.GetEndpoint() is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // Local admin/audit controllers continue through WMS authentication and authorization.
        await next(context);
    }

    private static string Destination(IConfiguration configuration, string service, string fallback)
    {
        var value = configuration[$"Services:{service}"] ?? fallback;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || uri.Host.Length == 0
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || uri.AbsolutePath != "/")
            throw new InvalidOperationException($"Services:{service} must be an absolute HTTP(S) origin without credentials, path, query or fragment.");
        return uri.AbsoluteUri;
    }
}

public static class ApiServiceProxyExtensions
{
    public static IServiceCollection AddApiServiceProxy(this IServiceCollection services)
    {
        services.AddHttpForwarder();
        services.AddSingleton<ApiServiceProxy>();
        services.AddSingleton(_ => new HttpMessageInvoker(new SocketsHttpHandler
        {
            UseProxy = false,
            UseCookies = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            EnableMultipleHttp2Connections = true,
            ConnectTimeout = TimeSpan.FromSeconds(15)
        }));
        return services;
    }

    public static IApplicationBuilder UseApiServiceProxy(this IApplicationBuilder app)
    {
        var proxy = app.ApplicationServices.GetRequiredService<ApiServiceProxy>();
        var forwarder = app.ApplicationServices.GetRequiredService<IHttpForwarder>();
        var client = app.ApplicationServices.GetRequiredService<HttpMessageInvoker>();
        return app.Use((context, next) => proxy.InvokeAsync(context, next, forwarder, client));
    }
}
