using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Order.Api.Data;
using Order.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = false);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDbContext<OrderDbContext>(o => o.UseNpgsql(
    builder.Configuration.GetConnectionString("Orders")
    ?? throw new InvalidOperationException("Set ConnectionStrings__Orders.")));
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<OrderDbContext>().SeedDevelopmentAsync();
}
app.MapOrderEndpoints();
app.Run();

