using System.Net.Http.Json;
using Inventory.BLL.Inbound;
using Inventory.Models.Inbound;

namespace Inventory.API.Clients;

public sealed class ReceivingOrderClient(IHttpClientFactory clients, IHttpContextAccessor context, IConfiguration configuration)
{
    public Task<ReceiptOrder[]> ListAsync(CancellationToken ct) => Send<ReceiptOrder[]>("", null, ct);
    public Task<ReceiptOrder> FindAsync(long id, CancellationToken ct) => Send<ReceiptOrder>($"/{id}", null, ct);
    public async Task ReserveAsync(long id, Guid token, CancellationToken ct) => await Send<object>($"/{id}/reserve", new { token }, ct);
    public async Task ReleaseAsync(long id, Guid token, CancellationToken ct) => await Send<object>($"/{id}/release", new { token }, ct);

    private async Task<T> Send<T>(string path, object? body, CancellationToken ct)
    {
        var url = new Uri(new Uri(configuration["Services:Procurement"] ?? "http://localhost:5136"), "/api/receiving-orders" + path);
        using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, url);
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.TryAddWithoutValidation("Cookie", context.HttpContext?.Request.Headers.Cookie.ToString());
        request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", context.HttpContext?.Request.Headers["X-CSRF-TOKEN"].ToString());
        using var response = await clients.CreateClient("receiving-orders").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode is 400 or 403 or 404 or 409)
            {
                var error = await response.Content.ReadFromJsonAsync<ServiceError>(cancellationToken: ct);
                throw new ReceiptException((int)response.StatusCode, error?.Message ?? "Không thể tiếp nhận PO này.");
            }
            throw new HttpRequestException("Procurement receiving service unavailable.");
        }
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return default!;
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct) ?? throw new HttpRequestException("Empty receiving order response.");
    }
    private sealed record ServiceError(string Message);
}
