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

internal static class ProfileApiTests
{
    public static async Task Main()
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !Directory.Exists(Path.Combine(directory.FullName,"Catalog.API"))) directory=directory.Parent;
        if(directory is null)throw new Exception("Missing Catalog.API content root.");
        await Run(directory.FullName);
    }

    public static async Task Run(string root)
    {
        // Match the production registration without opening a database connection.
        var services = new ServiceCollection();
        services.AddDbContextFactory<WarehouseDbContext>(options => options.UseSqlServer(Settings("unused", "Warehouse")));
        await using (var provider = services.BuildServiceProvider())
        await using (var context = await provider.GetRequiredService<IDbContextFactory<WarehouseDbContext>>().CreateDbContextAsync())
            Check(context.Model.FindEntityType(typeof(Warehouse.Models.Entities.Warehouse)) is not null, "warehouse factory resolves configured context");
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_CATALOG_API",Path.Combine(root,"Catalog.API"));
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_WAREHOUSE_API",Path.Combine(root,"Warehouse.API"));
        var server=Environment.GetEnvironmentVariable("EWMS_PUTAWAY_PROFILE_TEST_SERVER");
        await using var catalog=Factory<global::Program>("Catalog",server);
        await using var warehouse=Factory<WarehouseService::Warehouse.API.Controllers.PutawayLayoutController>("Warehouse",server);
        await RoleGuards(catalog,"/api/catalog/storage-profiles/1");
        await RoleGuards(warehouse,"/api/warehouses/putaway-layout");
        if(!string.IsNullOrWhiteSpace(server))
        {
            // Integration checks are SELECT-only against the explicitly selected source databases.
            var settings=Settings(server,"Catalog");
            await using var connection=new SqlConnection(settings);
            await connection.OpenAsync();
            await using var command=new SqlCommand("SELECT TOP (1) ProductID FROM dbo.Product ORDER BY ProductID",connection);
            var productId=Convert.ToInt32(await command.ExecuteScalarAsync());
            Check(productId>0,"storage profile fixture contains a product");
            using var keeper=Client(catalog,"staff1");
            using var profileResponse=await keeper.GetAsync($"/api/catalog/storage-profiles/{productId}");
            await Expect(profileResponse,200,"assigned staff storage profile");
            var profile=await profileResponse.Content.ReadFromJsonAsync<ProductStorageProfile>();
            Check(profile is {LengthCm:>0,WidthCm:>0,HeightCm:>0,GrossWeightKg:>0,CategoryId:>0,BrandId:>0}
                && Convert.FromBase64String(profile.RowVersion).Length==8,"authoritative profile fields and version");
            foreach(var warehouseId in new[]{1,2})
            {
                using var staff=Client(warehouse,"staff"+warehouseId);
                using var response=await staff.GetAsync("/api/warehouses/putaway-layout?warehouseId="+(warehouseId==1?2:1));
                await Expect(response,200,"assigned staff warehouse layout");
                var layout=await response.Content.ReadFromJsonAsync<PutawayLayout>();
                Check(layout is not null && layout.Warehouse.WarehouseId==warehouseId
                    && layout.Locations.Length>0 && layout.Locations.All(x=>x.WarehouseId==warehouseId),
                    "warehouse claim wins over forged query scope");
            }
        }
        else Console.WriteLine("SKIP profile SQL reads: set EWMS_PUTAWAY_PROFILE_TEST_SERVER to a configured source SQL Server.");
        Console.WriteLine("PASS Catalog/Warehouse storage read API role and warehouse-claim guards.");
    }

    private static WebApplicationFactory<T> Factory<T>(string domain,string? server) where T:class
        => new WebApplicationFactory<T>().WithWebHostBuilder(builder=>
        {
            builder.ConfigureLogging(logging=>logging.ClearProviders());
            builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>
            {[$"ConnectionStrings:{domain}"]=Settings(server??"unused",domain)}));
            builder.ConfigureServices(services=>services.AddHttpClient("identity-session")
                .ConfigurePrimaryHttpMessageHandler(()=>new IdentityStub()));
        });
    private static string Settings(string server,string domain)=>new SqlConnectionStringBuilder
    {DataSource=server,InitialCatalog=domain+"DB",IntegratedSecurity=true,Encrypt=true,TrustServerCertificate=true,ConnectTimeout=5}.ConnectionString;
    private static async Task RoleGuards<T>(WebApplicationFactory<T> factory,string route) where T:class
    {
        foreach(var role in new[]{"","admin","buyer","unassigned"})
        {
            using var client=Client(factory,role);
            using var response=await client.GetAsync(route);
            await Expect(response,role==""?401:403,"storage read role/assignment "+role);
        }
    }
    private static HttpClient Client<T>(WebApplicationFactory<T> factory,string role) where T:class
    {
        var client=factory.CreateClient(new(){AllowAutoRedirect=false});
        if(role.Length>0)client.DefaultRequestHeaders.Add("Cookie","EWMS.Session="+role);
        return client;
    }
    private static async Task Expect(HttpResponseMessage response,int status,string label)
        => Check((int)response.StatusCode==status,$"{label}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    private static void Check(bool value,string message)
    {if(!value)throw new Exception("FAIL: "+message);}
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
