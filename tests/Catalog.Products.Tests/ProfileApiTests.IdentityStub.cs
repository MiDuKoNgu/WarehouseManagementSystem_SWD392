extern alias WarehouseService;
using System.Net;
using System.Net.Http.Json;
using Catalog.Models.Products;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Warehouse.DAL;
using Warehouse.Models.Lookups;

namespace Catalog.Products.Tests;

internal static partial class ProfileApiTests
{
    private sealed class IdentityStub:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var cookie=string.Join(";",request.Headers.GetValues("Cookie"));
            var role=cookie.Contains("admin")?"ADMIN":cookie.Contains("buyer")?"PURCHASING_STAFF":"WAREHOUSE_STAFF";
            int? warehouseId=cookie.Contains("unassigned")?null:cookie.Contains("staff2")?2:1;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {Content=JsonContent.Create(new {userId=22,username="profile-test",fullName="Profile Reader",role,warehouseId})});
        }
    }
}
