using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Procurement.BLL.PurchaseOrders;
using Procurement.Models.PurchaseOrders;
using Procurement.Models.Receiving;

namespace Procurement.Tests;

internal sealed class IdentityStub : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("validate-csrf"))
            return Task.FromResult(new HttpResponseMessage(request.Headers.TryGetValues("X-CSRF-TOKEN", out var tokens) && tokens.Single() == "valid" ? HttpStatusCode.NoContent : HttpStatusCode.BadRequest));
        var cookie = request.Headers.TryGetValues("Cookie", out var cookies) ? cookies.Single() : "";
        var role = cookie.Contains("=admin") ? "ADMIN" : cookie.Contains("=warehouse") ? "WAREHOUSE_STAFF" : "PURCHASING_STAFF";
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new
        {
            userId = cookie.Contains("=buyer2") ? 23 : 22, username = "buyer", fullName = "Buyer Test", role,
            warehouseId = role == "WAREHOUSE_STAFF" && !cookie.Contains("unassigned") ? (int?)1 : null
        }) };
        return Task.FromResult(response);
    }
}
