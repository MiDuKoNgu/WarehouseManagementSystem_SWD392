using System.Net;
using System.Net.Http.Json;
using Inventory.API.Clients;
using Inventory.BLL.Inbound;
using Inventory.Models.Inbound;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Inventory.Inbound.Tests;

internal static partial class InboundTests
{
    private sealed class IdentityHandler:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            if(request.RequestUri!.AbsolutePath.EndsWith("validate-csrf"))return Task.FromResult(new HttpResponseMessage(
                request.Headers.TryGetValues("X-CSRF-TOKEN",out var values)&&values.Single()=="valid"?HttpStatusCode.NoContent:HttpStatusCode.BadRequest));
            var cookie=request.Headers.GetValues("Cookie").Single();
            var role=cookie.Contains("admin")?"ADMIN":cookie.Contains("buyer")?"PURCHASING_STAFF":"WAREHOUSE_STAFF";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(new
                {userId=22,username="warehouse",fullName="Warehouse Test",role,warehouseId=cookie.Contains("unassigned")?(int?)null:1})});
        }
    }
}
