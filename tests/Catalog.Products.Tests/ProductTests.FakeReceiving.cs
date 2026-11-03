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
    private sealed class FakeReceiving:IReceivingSummaryRepository
    {public Task<PurchaseOrderReceivingSummary> GetAsync(long id,CancellationToken ct)=>Task.FromResult(new PurchaseOrderReceivingSummary(id,false,0,[]));}
}
