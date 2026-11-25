using KitchenFlow.KitchenWorker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Order.Api.Data;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDbContext<OrderDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Orders")
    ?? throw new InvalidOperationException("Set ConnectionStrings__Orders.")));
builder.Services.AddHostedService<KitchenConsumer>();
await builder.Build().RunAsync();
