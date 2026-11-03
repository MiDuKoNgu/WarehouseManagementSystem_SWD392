extern alias InventoryService;
using System.Net;
using System.Net.Http.Json;
using Catalog.BLL.Products;
using Catalog.Models.Products;
using Inventory.DAL.Receiving;
using Inventory.Models.Receiving;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Catalog.Products.Tests;

public static partial class ProductTests
{
    private sealed class SessionStub:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            if(request.RequestUri!.AbsolutePath.EndsWith("validate-csrf")) return Task.FromResult(new HttpResponseMessage(request.Headers.TryGetValues("X-CSRF-TOKEN",out var values)&&values.Single()=="valid"?HttpStatusCode.NoContent:HttpStatusCode.BadRequest));
            var cookie=string.Join(";",request.Headers.GetValues("Cookie"));var role=cookie.Contains("admin")?"ADMIN":cookie.Contains("keeper")?"WAREHOUSE_STAFF":"PURCHASING_STAFF";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(new{userId=22,username="test",fullName="Product Tester",role,warehouseId=1})});
        }
    }
}
