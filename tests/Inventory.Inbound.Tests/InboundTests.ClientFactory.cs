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
    private sealed class ClientFactory(HttpMessageHandler handler):IHttpClientFactory
    {public HttpClient CreateClient(string name)=>new(handler,false);}
}
