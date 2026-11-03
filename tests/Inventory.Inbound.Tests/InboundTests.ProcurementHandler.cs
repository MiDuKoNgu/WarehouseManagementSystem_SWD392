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
    private sealed class ProcurementHandler:HttpMessageHandler
    {
        public bool Fail;public string? Cookie;public string? Csrf;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Cookie=request.Headers.GetValues("Cookie").Single();Csrf=request.Headers.GetValues("X-CSRF-TOKEN").Single();
            return Task.FromResult(Fail?new HttpResponseMessage(HttpStatusCode.Conflict){Content=JsonContent.Create(new{message="PO closed"})}:
                request.Method==HttpMethod.Post?new(HttpStatusCode.NoContent):new(HttpStatusCode.OK){Content=JsonContent.Create(Order())});
        }
    }
}
