using Microsoft.AspNetCore.Mvc;
using Procurement.API.Clients;
using Procurement.BLL.PurchaseOrders;
using Procurement.DAL.PurchaseOrders;
using Procurement.DAL.Receiving;
using Procurement.Excel;
using Procurement.Models.PurchaseOrders;
using WMS.ServiceDefaults;
using WMS.Audit;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
    options.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult(new { message = "Dữ liệu gửi lên không hợp lệ. Kiểm tra các trường bắt buộc và định dạng ngày/số lượng." }));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddEwmsServiceSecurity(builder.Configuration);
builder.Services.AddScoped(_ => new SqlAuditReader(
    builder.Configuration.GetConnectionString("Procurement") ?? throw new ServiceUnavailableException("Procurement connection is not configured."), "procurement"));
builder.Services.AddHttpClient("purchase-order-lookups", client => client.Timeout = TimeSpan.FromSeconds(15))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
builder.Services.AddScoped<IPurchaseOrderLookup, PurchaseOrderLookupClient>();
builder.Services.AddScoped<IPurchaseOrderRepository>(_ => new SqlPurchaseOrderRepository(
    builder.Configuration.GetConnectionString("Procurement") ?? throw new InvalidOperationException("Procurement database connection is not configured.")));
builder.Services.AddScoped(_ => new SqlReceivingReservationRepository(
    builder.Configuration.GetConnectionString("Procurement") ?? throw new ServiceUnavailableException("Procurement connection is not configured.")));
builder.Services.AddSingleton<IPurchaseOrderWorkbook, PurchaseOrderWorkbook>();
builder.Services.AddSingleton<ImportPreviewStore>();
builder.Services.AddScoped<PurchaseOrderService>();

var app = builder.Build();
app.UseEwmsServiceErrors();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (PurchaseOrderValidationException exception) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { message = exception.Message, errors = exception.Errors });
    }
    catch (WorkbookFormatException exception) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { message = exception.Message });
    }
    catch (ImportConflictException exception) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { message = exception.Message });
    }
    catch (PurchaseOrderConflictException exception) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { message = exception.Message });
    }
    catch (PurchaseOrderNotFoundException exception) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsJsonAsync(new { message = exception.Message });
    }
    catch (Exception exception) when ((exception is HttpRequestException or TaskCanceledException) && !context.RequestAborted.IsCancellationRequested && !context.Response.HasStarted)
    {
        context.Response.StatusCode = 503;
        await context.Response.WriteAsJsonAsync(new { message = "Không thể đọc danh mục hoặc kho. Vui lòng thử lại sau." });
    }
});
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
else app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
