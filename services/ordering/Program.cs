using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using MicroShop.Services.Ordering.Application;
using MicroShop.Services.Ordering.Infrastructure;
using MicroShop.Services.Ordering.Domain;

var builder = WebApplication.CreateBuilder(args);

// EF Core + Npgsql
var cs = builder.Configuration.GetConnectionString("Default")
         ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
         ?? "Host=postgres-ordering;Port=5432;Database=orderingdb;Username=ordering;Password=ordering";
builder.Services.AddDbContext<OrderingDbContext>(o => o.UseNpgsql(cs));

// MassTransit + RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumer<CheckoutRequestedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        var host = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "rabbitmq";
        var user = builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest";
        var pass = builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest";

        cfg.Host(host, "/", h => { h.Username(user); h.Password(pass); });
        cfg.ConfigureEndpoints(ctx);
    });
});

// OTel → Jaeger
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("ordering-service"))
    .WithTracing(t => t.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddOtlpExporter());

// Health
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health").AllowAnonymous();

// простые read-only эндпойнты
app.MapGet("/api/v1/orders/{userId:guid}", async (Guid userId, OrderingDbContext db) =>
{
    var orders = await db.Orders.Include(o => o.Items)
        .Where(o => o.UserId == userId)
        .OrderByDescending(o => o.CreatedUtc)
        .ToListAsync();
    return Results.Ok(orders);
}).AllowAnonymous();

// авто-миграция
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
    var attempts = 0;
    while (true)
    {
        try { await db.Database.MigrateAsync(); break; }
        catch when (++attempts < 5) { await Task.Delay(TimeSpan.FromSeconds(2 * attempts)); }
    }
}

app.Run();
