using Microsoft.AspNetCore.Mvc;
using WMS.API.Audit;
using WMS.API.Gateway;
using WMS.Audit;
using WMS.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
    options.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult(new { message = "Bộ lọc Audit Log không hợp lệ. Vui lòng kiểm tra ngày, ID và số dòng mỗi trang." }));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddEwmsServiceSecurity(builder.Configuration);
builder.Services.AddHttpClient("audit-sources", client => client.Timeout = TimeSpan.FromSeconds(15))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
builder.Services.AddScoped<AuditGateway>();
builder.Services.AddApiServiceProxy();

var app = builder.Build();
app.UseEwmsServiceErrors();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (AuditValidationException exception) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { message = exception.Message, errors = exception.Errors });
    }
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Forward before WMS's audit session lookup: Identity owns login, CSRF and logout,
// including clearing a stale cookie when its user database is unavailable.
app.UseRouting();
app.UseApiServiceProxy();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
