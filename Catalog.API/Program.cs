using Catalog.DAL.Repositories;
using Catalog.BLL.Products;
using Microsoft.AspNetCore.Mvc;
using WMS.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult(new { message = "Dữ liệu sản phẩm hoặc bộ lọc không hợp lệ." }));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddEwmsServiceSecurity(builder.Configuration);
builder.Services.AddScoped<IProductRepository>(_ => new SqlProductRepository(
    builder.Configuration.GetConnectionString("Catalog") ?? throw new ServiceUnavailableException("Catalog connection is not configured.")));
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped(_ => new CatalogLookupRepository(
    builder.Configuration.GetConnectionString("Catalog")
    ?? throw new ServiceUnavailableException("Catalog connection is not configured.")));

var app = builder.Build();
app.UseEwmsServiceErrors();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (ProductValidationException error) when (!context.Response.HasStarted)
    { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { message = error.Message, errors = error.Errors }); }
    catch (ProductConflictException error) when (!context.Response.HasStarted)
    { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { message = error.Message }); }
    catch (ProductNotFoundException error) when (!context.Response.HasStarted)
    { context.Response.StatusCode = 404; await context.Response.WriteAsJsonAsync(new { message = error.Message }); }
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
