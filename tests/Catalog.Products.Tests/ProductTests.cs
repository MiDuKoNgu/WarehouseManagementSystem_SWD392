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

public static class ProductTests
{
    public static async Task<int> Main()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName,"Catalog.API"))) root=root.Parent;
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_CATALOG_API",Path.Combine(root!.FullName,"Catalog.API"));
        var repo=new FakeProducts();
        await using var factory=new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder=>
        {
            builder.ConfigureLogging(logging=>logging.ClearProviders());
            builder.ConfigureServices(services=>
            {
                services.RemoveAll<IProductRepository>(); services.AddSingleton<IProductRepository>(repo);
                services.AddHttpClient("identity-session").ConfigurePrimaryHttpMessageHandler(()=>new SessionStub());
            });
        });
        foreach(var role in new[]{"","admin","keeper"})
        {
            using var denied=Client(factory,role);
            foreach(var route in new[]{"/api/products","/api/products/options","/api/products/1"})
                await Expect(await denied.GetAsync(route),role==""?401:403,"product role guard");
        }
        using var buyer=Client(factory,"buyer");
        await Expect(await buyer.PostAsJsonAsync("/api/products",Valid()),400,"product CSRF required");
        buyer.DefaultRequestHeaders.Add("X-CSRF-TOKEN","valid");
        var invalid=Valid(); invalid.LengthCm=0;
        await Expect(await buyer.PostAsJsonAsync("/api/products",invalid),400,"positive dimensions");
        invalid=Valid(); invalid.GrossWeightKg=1.2345m;
        await Expect(await buyer.PostAsJsonAsync("/api/products",invalid),400,"weight precision");
        invalid=Valid(); invalid.SupplierIds=[1,1];
        await Expect(await buyer.PostAsJsonAsync("/api/products",invalid),400,"supplier duplicates");
        invalid=Valid(); invalid.ProductCode="bad code";
        await Expect(await buyer.PostAsJsonAsync("/api/products",invalid),400,"code format");
        var createdResponse=await buyer.PostAsJsonAsync("/api/products",Valid());
        await Expect(createdResponse,201,"create product");
        var created=(await createdResponse.Content.ReadFromJsonAsync<ProductDetail>())!;
        Check(created.CreatedByUserId==22 && created.ProductCode=="TEST-AC-01","server creator and normalized code");
        await Expect(await buyer.GetAsync("/api/products/1"),200,"product detail");
        await Expect(await buyer.GetAsync("/api/products?search=AC&categoryId=1&brandId=1&supplierId=1&status=active&page=2&pageSize=10"),200,"product filters accepted");
        Check(repo.LastQuery is {Page:2,PageSize:10,Status:"ACTIVE",Search:"AC",SupplierId:1},"filters passed to repository");
        await Expect(await buyer.GetAsync("/api/products?page=0"),400,"page validation");
        var update=Valid(); update.RowVersion=created.RowVersion; update.ProductName="Updated";
        await Expect(await buyer.PutAsJsonAsync("/api/products/1",update),200,"update product");
        await Expect(await buyer.PutAsJsonAsync("/api/products/1",update),409,"stale version rejected");
        update.RowVersion=repo.Product!.RowVersion; update.ProductCode="CHANGE-CODE";
        await Expect(await buyer.PutAsJsonAsync("/api/products/1",update),400,"immutable product code");
        await Expect(await buyer.DeleteAsync("/api/products/1"),405,"no product delete");
        await InventoryRoleTests(root.FullName);
        if(Environment.GetEnvironmentVariable("EWMS_PRODUCT_TEST_SERVER") is {Length:>0} server) await SqlProductTests.Run(server);
        Console.WriteLine("PASS: Purchasing-only Product create/edit/list/detail/filter, CSRF, precision/code/supplier validation, concurrency, and receiving-summary roles.");
        return 0;
    }
    private static async Task InventoryRoleTests(string root)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_INVENTORY_API",Path.Combine(root,"Inventory.API"));
        await using var factory=new WebApplicationFactory<InventoryService::Program>().WithWebHostBuilder(builder=>
        {
            builder.ConfigureLogging(logging=>logging.ClearProviders());
            builder.ConfigureServices(services=>
            {
                services.RemoveAll<IReceivingSummaryRepository>();services.AddSingleton<IReceivingSummaryRepository>(new FakeReceiving());
                services.AddHttpClient("identity-session").ConfigurePrimaryHttpMessageHandler(()=>new SessionStub());
            });
        });
        foreach(var role in new[]{"","admin","keeper","buyer"})
        {
            using var client=Client(factory,role);
            await Expect(await client.GetAsync("/api/inventory/purchase-orders/1/receiving-summary"),role==""?401:role=="buyer"?200:403,"receiving summary role");
        }
    }
    public static ProductInput Valid()=>new(){ProductCode=" test-ac-01 ",ProductName="Điều hòa test",Model="TEST",Unit="Chiếc",CategoryId=1,BrandId=1,Status="ACTIVE",LengthCm=80,WidthCm=30,HeightCm=25,GrossWeightKg=20,SupplierIds=[1]};
    public static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);}
    private static async Task Expect(HttpResponseMessage response,int status,string message)=>Check((int)response.StatusCode==status,$"{message}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    private static HttpClient Client<T>(WebApplicationFactory<T> factory,string role) where T:class
    {var client=factory.CreateClient(new(){AllowAutoRedirect=false});if(role!="")client.DefaultRequestHeaders.Add("Cookie","EWMS.Session="+role);return client;}
    private sealed class SessionStub:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            if(request.RequestUri!.AbsolutePath.EndsWith("validate-csrf")) return Task.FromResult(new HttpResponseMessage(request.Headers.TryGetValues("X-CSRF-TOKEN",out var values)&&values.Single()=="valid"?HttpStatusCode.NoContent:HttpStatusCode.BadRequest));
            var cookie=string.Join(";",request.Headers.GetValues("Cookie"));var role=cookie.Contains("admin")?"ADMIN":cookie.Contains("keeper")?"WAREHOUSE_STAFF":"PURCHASING_STAFF";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(new{userId=22,username="test",fullName="Product Tester",role,warehouseId=1})});
        }
    }
    private sealed class FakeReceiving:IReceivingSummaryRepository
    {public Task<PurchaseOrderReceivingSummary> GetAsync(long id,CancellationToken ct)=>Task.FromResult(new PurchaseOrderReceivingSummary(id,false,0,[]));}
    private sealed class FakeProducts:IProductRepository
    {
        public ProductDetail? Product;public ProductQuery? LastQuery;private int _version;
        public Task<ProductPage> ListAsync(ProductQuery query,CancellationToken ct){LastQuery=query;return Task.FromResult(new ProductPage(Product is null?[]:[Product],Product is null?0:1,query.Page,query.PageSize));}
        public Task<ProductDetail?> FindAsync(int id,CancellationToken ct)=>Task.FromResult(id==1?Product:null);
        public Task<ProductOptions> OptionsAsync(CancellationToken ct)=>Task.FromResult(new ProductOptions([new(1,"Điều hòa")],[new(1,"Daikin")],[new(1,"Supplier")]));
        public Task<ProductDetail> SaveAsync(int? id,ProductInput input,int actor,byte[]? version,CancellationToken ct)
        {
            if(id is not null && Product is null)throw new ProductNotFoundException();
            if(id is not null && Product!.ProductCode!=input.ProductCode)throw new ProductValidationException(["Mã không được đổi."]);
            if(id is not null && Product!.RowVersion!=Convert.ToBase64String(version!))throw new ProductConflictException("Stale version");
            if(id is null && Product is not null)throw new ProductConflictException("Duplicate code");
            Product=new(1,input.ProductCode,input.ProductName,input.Model,input.Unit,1,"Điều hòa",1,"Daikin",input.Status,input.Specifications,input.LengthCm,input.WidthCm,input.HeightCm,input.GrossWeightKg,[new(1,"Supplier")],Product?.CreatedByUserId??actor,DateTime.UtcNow,null,Convert.ToBase64String(BitConverter.GetBytes((long)++_version)));
            return Task.FromResult(Product);
        }
    }
}
