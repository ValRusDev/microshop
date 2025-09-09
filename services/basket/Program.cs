using System.Text;
using System.Text.Json.Serialization;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using MicroShop.Services.Basket.Domain;
using MicroShop.Services.Basket.Infrastructure;
using MicroShop.Contracts;
using MicroShop.Services.Basket.Endpoints;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MicroShop.Services.Basket.Infrastructure.Health;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// JWT
var jwt = builder.Configuration.GetSection("Jwt");
var keyRaw = jwt["Key"]!;
byte[] keyBytes;
try { keyBytes = Convert.FromBase64String(keyRaw); } catch { keyBytes = Encoding.UTF8.GetBytes(keyRaw); }
if (keyBytes.Length < 32) throw new InvalidOperationException("Jwt:Key must be >= 32 bytes");
var signingKey = new SymmetricSecurityKey(keyBytes);

builder.Services.AddAuthentication().AddJwtBearer(o =>
{
    o.TokenValidationParameters = new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = jwt["Issuer"],
        ValidAudience = jwt["Audience"],
        IssuerSigningKey = signingKey
    };
});
builder.Services.AddAuthorization();

// Redis Cache
var redisCfg = builder.Configuration.GetValue<string>("Redis:Configuration") ?? "redis:6379";
builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisCfg);
builder.Services.AddSingleton<IBasketStore, RedisBasketStore>();

// MassTransit + RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        var host = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "rabbitmq";
        var user = builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest";
        var pass = builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest";

        cfg.Host(host, "/", h =>
        {
            h.Username(user);
            h.Password(pass);
        });

        cfg.ConfigureEndpoints(ctx);
    });
});

// OpenTelemetry → Jaeger
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("basket-service"))
    .WithTracing(t => t.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddOtlpExporter());

builder.Services.AddHealthChecks()
    .AddCheck<RedisCacheHealthCheck>("redis", tags: new[] { "ready" })
    .AddCheck<RabbitMqHealthCheck>("rabbit", tags: new[] { "ready" });

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

var group = app.MapGroup("/api/v1/basket").RequireAuthorization();

// GET корзины
group.MapGet("/{userId:guid}", async (Guid userId, IBasketStore store) =>
{
    var b = await store.GetAsync(userId);
    return Results.Ok(b);
});

group.MapPost("/{userId:guid}/items", async (Guid userId, UpsertItemDto dto, IBasketStore store) =>
{
    var basket = await store.GetAsync(userId);
    var idx = basket.Items.FindIndex(i => i.ProductId == dto.ProductId);
    var item = new BasketItem(dto.ProductId, dto.Quantity, dto.UnitPrice);
    if (idx >= 0) basket.Items[idx] = item; else basket.Items.Add(item);
    await store.SaveAsync(basket);
    return Results.Ok(basket);
});

// удалить позицию
group.MapDelete("/{userId:guid}/items/{productId:guid}", async (Guid userId, Guid productId, IBasketStore store) =>
{
    var basket = await store.GetAsync(userId);
    basket.Items.RemoveAll(i => i.ProductId == productId);
    await store.SaveAsync(basket);
    return Results.NoContent();
});

// checkout → публикация события и очистка корзины
group.MapPost("/{userId:guid}/checkout", async (Guid userId, IBasketStore store, IPublishEndpoint bus) =>
{
    var basket = await store.GetAsync(userId);
    if (basket.Items.Count == 0) return Results.BadRequest(new { message = "Basket is empty" });

    var items = basket.Items.Select(i => new BasketItemDto(i.ProductId, i.Quantity, i.UnitPrice)).ToList();
    var evt = new CheckoutRequested(userId, items, basket.Total);
    await bus.Publish(evt);

    await store.ClearAsync(userId);
    return Results.Accepted($"/api/v1/orders", new { message = "Checkout requested", basket.Total });
});

app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions {
    Predicate = r => r.Tags.Contains("ready")
}).AllowAnonymous();

app.Run();
