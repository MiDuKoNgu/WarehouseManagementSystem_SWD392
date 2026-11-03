using System.Net;
using System.Net.Http.Json;
using Inventory.BLL.Putaway;
using Inventory.Models.Putaway;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Inventory.Inbound.Tests;

internal static partial class PutawayApiTests
{
    private sealed class Identity : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("validate-csrf")) return Task.FromResult(new HttpResponseMessage(
                request.Headers.TryGetValues("X-CSRF-TOKEN", out var values) && values.Single() == "valid" ? HttpStatusCode.NoContent : HttpStatusCode.BadRequest));
            var session = request.Headers.GetValues("Cookie").Single();
            var role = session.Contains("admin") ? "ADMIN" : session.Contains("buyer") ? "PURCHASING_STAFF" : "WAREHOUSE_STAFF";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new
                { userId = 22, username = "warehouse", fullName = "Warehouse API Test", role, warehouseId = session.Contains("unassigned") ? (int?)null : 1 }) });
        }
    }
}
