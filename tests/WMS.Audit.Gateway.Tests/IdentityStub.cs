using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WMS.Audit;

namespace WMS.Audit.Gateway.Tests;

internal sealed class IdentityStub : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cookie = request.Headers.TryGetValues("Cookie", out var values) ? values.Single() : "";
        var role = cookie.Contains("=admin") ? "ADMIN" : cookie.Contains("=warehouse") ? "WAREHOUSE_STAFF" : "PURCHASING_STAFF";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { userId = 1, username = "admin", fullName = "Admin", role, warehouseId = (int?)null }) });
    }
}
