using Warehouse.DAL.Repositories;
using WMS.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddEwmsServiceSecurity(builder.Configuration);
builder.Services.AddScoped(_ => new WarehouseLookupRepository(
    builder.Configuration.GetConnectionString("Warehouse")
    ?? throw new ServiceUnavailableException("Warehouse connection is not configured.")));

var app = builder.Build();
app.UseEwmsServiceErrors();

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
