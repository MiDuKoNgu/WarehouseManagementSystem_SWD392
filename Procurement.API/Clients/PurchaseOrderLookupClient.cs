using System.Net.Http.Json;
using Procurement.BLL.PurchaseOrders;
using Procurement.Models.PurchaseOrders;

namespace Procurement.API.Clients;

public sealed partial class PurchaseOrderLookupClient(IHttpClientFactory clients, IHttpContextAccessor context,
    IConfiguration configuration) : IPurchaseOrderLookup
{
    public async Task<PurchaseOrderOptions> GetOptionsAsync(CancellationToken ct)
    {
        var catalog = GetAsync<CatalogOptions>("Catalog", "http://localhost:5254", "/api/catalog/options", ct);
        var warehouses = GetAsync<WarehouseOption[]>("Warehouse", "http://localhost:5104", "/api/warehouses/options", ct);
        await Task.WhenAll(catalog, warehouses);
        return new(catalog.Result.Suppliers, warehouses.Result, catalog.Result.Products);
    }

    public async Task<string?> GetCreatorNameAsync(int userId, CancellationToken ct)
    {
        var display = await GetAsync<DisplayName>("Identity", "http://localhost:5140", $"/api/auth/users/{userId}/display-name", ct);
        return display.FullName;
    }

    public Task<ReceivingSummary> GetReceivingSummaryAsync(long purchaseOrderId, CancellationToken ct) =>
        GetAsync<ReceivingSummary>("Inventory", "http://localhost:5159", $"/api/inventory/purchase-orders/{purchaseOrderId}/receiving-summary", ct);

    private async Task<T> GetAsync<T>(string service, string fallback, string path, CancellationToken ct)
    {
        var baseUrl = configuration[$"Services:{service}"] ?? fallback;
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(baseUrl), path));
        var cookie = context.HttpContext?.Request.Headers.Cookie.ToString();
        if (!string.IsNullOrEmpty(cookie)) request.Headers.TryAddWithoutValidation("Cookie", cookie);
        using var response = await clients.CreateClient("purchase-order-lookups").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Lookup service {service} returned {(int)response.StatusCode}.");
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
            ?? throw new HttpRequestException($"Lookup service {service} returned empty data.");
    }
}
