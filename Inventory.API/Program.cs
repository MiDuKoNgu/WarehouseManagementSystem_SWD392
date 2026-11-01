using WMS.Audit;
using Inventory.DAL.Receiving;
using WMS.ServiceDefaults;
using Inventory.API.Clients;
using Inventory.BLL.Inbound;
using Inventory.DAL.Inbound;
using Inventory.Models.Inbound;
using Inventory.BLL.Putaway;
using Inventory.DAL.Putaway;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddEwmsServiceSecurity(builder.Configuration);
builder.Services.AddScoped<IPutawayOperationGuard, CurrentSchemaPutawayGuard>();
builder.Services.AddScoped<IPutawayRepository>(provider => new SqlPutawayRepository(
    builder.Configuration.GetConnectionString("Inventory") ?? throw new ServiceUnavailableException("Inventory connection is not configured."),
    provider.GetRequiredService<IPutawayOperationGuard>(),
    builder.Configuration["Putaway:CatalogDatabase"] ?? "CatalogDB",
    builder.Configuration["Putaway:WarehouseDatabase"] ?? "WarehouseDB"));
builder.Services.AddScoped<PutawayService>();
builder.Services.AddSingleton(new ReceiptPolicy(
    builder.Configuration.GetValue<bool>("Inbound:AllowMultipleDrafts"),
    builder.Configuration.GetValue<bool>("Inbound:AcceptDamaged"),
    builder.Configuration.GetValue<bool>("Inbound:AllowCancellation")));
builder.Services.AddScoped(provider => new SqlInboundRepository(
    builder.Configuration.GetConnectionString("Inventory") ?? throw new ServiceUnavailableException("Inventory connection is not configured."),
    provider.GetRequiredService<ReceiptPolicy>()));
builder.Services.AddScoped(_ => new ReceiptQueries(builder.Configuration.GetConnectionString("Inventory")
    ?? throw new ServiceUnavailableException("Inventory connection is not configured.")));
builder.Services.AddHttpClient("receiving-orders", client => client.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
builder.Services.AddScoped<ReceivingOrderClient>();
builder.Services.AddScoped<IReceivingSummaryRepository>(_ => new ReceivingSummaryRepository(
    builder.Configuration.GetConnectionString("Inventory") ?? throw new ServiceUnavailableException("Inventory connection is not configured.")));
builder.Services.AddScoped(_ => new SqlAuditReader(
    builder.Configuration.GetConnectionString("Inventory") ?? throw new ServiceUnavailableException("Inventory connection is not configured."), "inventory"));

var app = builder.Build();
app.UseEwmsServiceErrors();
app.Use(async (context,next) =>
{
    try { await next(); }
    catch (PutawayException error) when (!context.Response.HasStarted)
    { context.Response.StatusCode=error.StatusCode; await context.Response.WriteAsJsonAsync(new {message=error.Message,code=error.Code,currentLocationId=error.CurrentLocationId,currentLocationPath=error.CurrentLocationPath}); }
    catch (ReceiptException error) when (!context.Response.HasStarted)
    { context.Response.StatusCode=error.StatusCode; await context.Response.WriteAsJsonAsync(new {message=error.Message}); }
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
