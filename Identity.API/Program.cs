using System.Threading.RateLimiting;
using Identity.API.Authentication;
using Identity.BLL.Authentication;
using Identity.BLL.Security;
using Identity.DAL.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddScoped<LoginService>();
builder.Services.AddScoped<IUserRepository>(_ => new SqlUserRepository(
    builder.Configuration.GetConnectionString("Identity")
    ?? builder.Configuration["EWMS_IDENTITY_CONNECTION"]
    ?? throw new InvalidOperationException("Identity database connection is not configured.")));
builder.Services.AddScoped<SessionValidation>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "EWMS.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.SlidingExpiration = false;
    options.EventsType = typeof(SessionValidation);
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "EWMS.Csrf";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Bạn thử đăng nhập quá nhiều lần. Vui lòng đợi một phút." }, cancellationToken);
    };
});

var app = builder.Build();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (Exception exception) when ((exception is SqlException or InvalidOperationException) && !context.Response.HasStarted)
    {
        if (exception is SqlException sql)
            app.Logger.LogError("Identity SQL request failed: number={SqlNumber}, state={SqlState}, class={SqlClass}", sql.Number, sql.State, sql.Class);
        else app.Logger.LogError("Identity request failed: {ErrorType}", exception.GetType().Name);
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { message = "Không thể kết nối dịch vụ tài khoản. Vui lòng thử lại sau." });
    }
});
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
else { app.UseHttpsRedirection(); }
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
