extern alias InventoryService;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WMS.Audit;

namespace WMS.Audit.Source.Tests;

public static partial class SourceTests
{

    private sealed class IdentityStub : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var cookie = string.Join(";", request.Headers.GetValues("Cookie"));
            var role = cookie.Contains("admin") ? "ADMIN" : cookie.Contains("buyer") ? "PURCHASING_STAFF" : "WAREHOUSE_STAFF";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { userId = 1, username = "source-test", fullName = "Source Test", role, warehouseId = 1 }) });
        }
    }
}
