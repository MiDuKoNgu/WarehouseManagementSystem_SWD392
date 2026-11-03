using System.Net;
using Yarp.ReverseProxy.Forwarder;

namespace WMS.API.Gateway;

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
