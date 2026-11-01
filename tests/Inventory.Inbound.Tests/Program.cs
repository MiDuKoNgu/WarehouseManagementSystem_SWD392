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

internal static class InboundTests
{
    public static async Task Main()
    {
        Rules();
        PutawayRuleTests.Run();
        await ApiSecurity();
        await PutawayApiTests.Run();
        await ServiceClient();
        var server = Environment.GetEnvironmentVariable("EWMS_INBOUND_TEST_SERVER");
        if (string.IsNullOrWhiteSpace(server)) Console.WriteLine("SKIP SQL: set EWMS_INBOUND_TEST_SERVER for a generated disposable database.");
        else await SqlTests.Run(server);
        var putawayServer = Environment.GetEnvironmentVariable("EWMS_PUTAWAY_TEST_SERVER");
        if (string.IsNullOrWhiteSpace(putawayServer)) Console.WriteLine("SKIP Putaway SQL: set EWMS_PUTAWAY_TEST_SERVER for three generated disposable databases.");
        else await PutawaySqlTests.Run(putawayServer);
        Console.WriteLine("PASS Inbound rules, API authorization/warehouse/CSRF, Procurement forwarding and errors.");
    }

    internal static readonly DateOnly Today = new(2026, 11, 1);
    internal static readonly ReceiptActor Actor = new(22,"Warehouse Test",1);
    internal static readonly ReceiptPolicy Policy = new(true,true,true);
    internal static readonly SaveReceiptItem Valid = new(1," MiXeD-123 ",11,2026,"GOOD",null,"MANUAL",Convert.ToBase64String(new byte[8]));
    internal static ReceiptOrder Order(long id=1, int quantity=3) => new(id,$"PO-{id}",1,"Supplier",1,"Warehouse","CONFIRMED",[new(1,1,"SKU-1","Product",quantity,0)]);
    internal static void Check(bool value,string name) { if(!value) throw new Exception("FAIL "+name); }
    internal static void Reject(Action action,int expected,string name)
    {
        try { action(); } catch(ReceiptException error) when(error.StatusCode==expected) {return;}
        throw new Exception("FAIL expected rejection: "+name);
    }
    private static void Rules()
    {
        Check(InboundRules.Validate(Valid,Policy,Today).Serial=="MiXeD-123","trim preserves serial case");
        foreach(var serial in new[]{""," ",new string('a',101),"ABC\nDEF"})
            Reject(()=>InboundRules.Validate(Valid with {Serial=serial},Policy,Today),400,"invalid serial");
        Check(InboundRules.Validate(Valid with {Serial=new string('A',100)},Policy,Today).Serial.Length==100,"schema boundary serial");
        foreach(var month in new[]{0,13,-1}) Reject(()=>InboundRules.Validate(Valid with {ManufacturingMonth=month},Policy,Today),400,"month range");
        foreach(var year in new[]{1899,2027,99999}) Reject(()=>InboundRules.Validate(Valid with {ManufacturingYear=year},Policy,Today),400,"year range/future");
        Reject(()=>InboundRules.Validate(Valid with {ManufacturingMonth=12},Policy,Today),400,"future month");
        Reject(()=>InboundRules.Validate(Valid with {PoLineId=0},Policy,Today),400,"line required");
        Reject(()=>InboundRules.Validate(Valid with {ReceivedCondition="UNKNOWN"},Policy,Today),400,"condition enum");
        Reject(()=>InboundRules.Validate(Valid with {ReceivedCondition="REJECTED",ConditionNote="Severe damage"},Policy,Today),400,"severe rejected goods cannot enter inventory");
        Reject(()=>InboundRules.Validate(Valid with {ReceivedCondition="DAMAGED"},Policy,Today),400,"damaged note required");
        Reject(()=>InboundRules.Validate(Valid with {ReceivedCondition="DAMAGED",ConditionNote="Broken"},new(),Today),400,"damaged policy");
        Check(InboundRules.Validate(Valid with {ReceivedCondition="DAMAGED",ConditionNote=" Broken "},Policy,Today).ConditionNote=="Broken","damaged description retained");
        Reject(()=>InboundRules.Validate(Valid with {ConditionNote=new string('x',1001)},Policy,Today),400,"note bound");
        foreach(var method in new[]{"CAMERA","IMAGE_UPLOAD","MANUAL"})
            Check(InboundRules.Validate(Valid with {InputMethod=method},Policy,Today).InputMethod==method,"input methods share validation");
        Reject(()=>InboundRules.Validate(Valid with {InputMethod="USB"},Policy,Today),400,"method enum");
        foreach(var version in new[]{"","bad",Convert.ToBase64String(new byte[7])}) Reject(()=>InboundRules.Version(version),400,"version validation");
    }
    private static async Task ApiSecurity()
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null&&!Directory.Exists(Path.Combine(directory.FullName,"Inventory.API"))) directory=directory.Parent;
        if(directory is null) throw new Exception("Missing Inventory.API content root.");
        Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_INVENTORY_API",Path.Combine(directory.FullName,"Inventory.API"));
        await using var factory=new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder=>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>
                {["ConnectionStrings:Inventory"]="Server=unused;Database=unused;Integrated Security=true;Connect Timeout=1"}));
            builder.ConfigureLogging(log=>log.ClearProviders());
            builder.ConfigureServices(services=>services.AddHttpClient("identity-session").ConfigurePrimaryHttpMessageHandler(()=>new IdentityHandler()));
        });
        using var anonymous=factory.CreateClient(new(){AllowAutoRedirect=false});
        await Status(await anonymous.GetAsync("/api/inbounds"),401,"anonymous list");
        foreach(var role in new[]{"admin","buyer"})
        {
            using var client=Client(factory,role);
            await Status(await client.GetAsync("/api/inbounds"),403,"wrong role list");
            await Status(await client.PostAsJsonAsync("/api/inbounds",new CreateReceipt(1,null,Guid.NewGuid())),403,"wrong role write");
            await Status(await client.GetAsync("/api/inventory/pending-putaway"),403,"wrong role pending stock");
        }
        using var missing=Client(factory,"unassigned");
        await Status(await missing.GetAsync("/api/inbounds"),403,"assignment required before DB");
        await Status(await missing.GetAsync("/api/inbounds/orders"),403,"order selection assignment");
        await Status(await missing.GetAsync("/api/inventory/pending-putaway"),403,"pending assignment");
        using var warehouse=Client(factory,"warehouse");
        await Status(await warehouse.PostAsJsonAsync("/api/inbounds",new CreateReceipt(1,null,Guid.NewGuid())),400,"CSRF required");
        warehouse.DefaultRequestHeaders.Add("X-CSRF-TOKEN","invalid");
        await Status(await warehouse.PostAsJsonAsync("/api/inbounds/1/complete",new ReceiptTransition(Valid.RowVersion)),400,"invalid CSRF rejected");
        warehouse.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");warehouse.DefaultRequestHeaders.Add("X-CSRF-TOKEN","valid");
        await Status(await warehouse.PostAsync("/api/inbounds",JsonContent.Create(new {purchaseOrderId="bad"})),400,"malformed request rejected");
    }
    private static HttpClient Client(WebApplicationFactory<global::Program> factory,string cookie)
    {
        var client=factory.CreateClient(new(){AllowAutoRedirect=false});client.DefaultRequestHeaders.Add("Cookie","EWMS.Session="+cookie);return client;
    }
    private static async Task Status(HttpResponseMessage response,int expected,string label)
    {Check((int)response.StatusCode==expected,$"{label}: expected {expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");}
    private static async Task ServiceClient()
    {
        var handler=new ProcurementHandler();
        var context=new DefaultHttpContext();context.Request.Headers.Cookie="EWMS.Session=warehouse";context.Request.Headers["X-CSRF-TOKEN"]="csrf-value";
        var client=new ReceivingOrderClient(new ClientFactory(handler),new HttpContextAccessor{HttpContext=context},new ConfigurationBuilder().Build());
        Check((await client.FindAsync(1,default)).PurchaseOrderId==1,"PO deserialized from owner service");
        await client.ReserveAsync(1,Guid.NewGuid(),default);
        Check(handler.Cookie=="EWMS.Session=warehouse"&&handler.Csrf=="csrf-value","session/CSRF forwarded");
        handler.Fail=true;
        try{await client.FindAsync(1,default);throw new Exception("Expected owner service conflict");}
        catch(ReceiptException error){Check(error.StatusCode==409&&error.Message=="PO closed","owner conflict preserved");}
    }
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
    private sealed class ClientFactory(HttpMessageHandler handler):IHttpClientFactory
    {public HttpClient CreateClient(string name)=>new(handler,false);}
}
