using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using MicroShop.Services.Ordering.Application;
using MicroShop.Services.Ordering.Infrastructure;
using MicroShop.Services.Catalog.Infrastructure.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// EF Core + Npgsql
var cs = builder.Configuration.GetConnectionString("Default")
         ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
         ?? "Host=postgres-ordering;Port=5432;Database=orderingdb;Username=ordering;Password=ordering";
builder.Services.AddDbContext<OrderingDbContext>(o => o.UseNpgsql(cs));

// MassTransit
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

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("ordering-service"))
    .WithTracing(t => t.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddOtlpExporter());

// Health
builder.Services.AddHealthChecks()
    .AddCheck<EfDbHealthCheck<OrderingDbContext>>("db", tags: new[] { "ready" })
    .AddCheck<RabbitMqHealthCheck>("rabbit", tags: new[] { "ready" });

var app = builder.Build();

app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions {
    Predicate = r => r.Tags.Contains("ready"),
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var payload = new {
            status = report.Status.ToString(),
            details = report.Entries.Select(kv => new {
                name = kv.Key,
                status = kv.Value.Status.ToString(),
                error  = kv.Value.Exception?.Message
            })
        };
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}).AllowAnonymous();

// read-only API: все заказы пользователя
app.MapGet("/api/v1/orders/{userId:guid}", async (Guid userId, OrderingDbContext db) =>
{
    var orders = await db.Orders.Include(o => o.Items)
        .Where(o => o.UserId == userId)
        .OrderByDescending(o => o.CreatedUtc)
        .ToListAsync();
    return Results.Ok(orders);
}).AllowAnonymous();

// авто-миграция (c простым ретраем)
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
    for (var attempt = 1; ; attempt++)
    {
        try { await db.Database.MigrateAsync(); break; }
        catch when (attempt < 5) { await Task.Delay(TimeSpan.FromSeconds(2 * attempt)); }
    }
}

app.Run();
