using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using MicroShop.Services.Catalog.Infrastructure;
using MicroShop.Services.Catalog.Domain;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MicroShop.Services.Catalog.Infrastructure.Health;

var builder = WebApplication.CreateBuilder(args);

// ===== DB (EF Core + Npgsql) =====
var cs = builder.Configuration.GetConnectionString("Default")
         ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
         ?? throw new InvalidOperationException("No connection string configured (ConnectionStrings:Default).");

builder.Services.AddDbContext<CatalogDbContext>(o => o.UseNpgsql(cs));

// ===== JWT AuthN/AuthZ =====
var jwt = builder.Configuration.GetSection("Jwt");
if (string.IsNullOrWhiteSpace(jwt["Key"]))
    throw new InvalidOperationException("Jwt:Key is not configured.");

byte[] keyBytes;
try { keyBytes = Convert.FromBase64String(jwt["Key"]!); }
catch { keyBytes = Encoding.UTF8.GetBytes(jwt["Key"]!); }
if (keyBytes.Length < 32)
    throw new InvalidOperationException("JWT key must be at least 32 bytes (256 bits) for HS256.");

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

// ===== OpenTelemetry → Jaeger (OTLP) =====
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("catalog-service"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter()); // читает OTEL_EXPORTER_OTLP_ENDPOINT (например, http://jaeger:4317)

builder.Services.AddHealthChecks()
    .AddCheck<EfDbHealthCheck<CatalogDbContext>>("db", tags: new[] { "ready" });

var app = builder.Build();

// ===== Middleware =====
app.UseAuthentication();
app.UseAuthorization();

// ===== Endpoints =====
// Публичный GET (анонимно)
app.MapGet("/api/v1/products", async (CatalogDbContext db) =>
    await db.Products.AsNoTracking().ToListAsync()
).AllowAnonymous();

// Защищённый POST (нужен Bearer-токен)
app.MapPost("/api/v1/products", async (CatalogDbContext db, Product p) =>
{
    db.Add(p);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/products/{p.Id}", p);
}).RequireAuthorization();

app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions {
    Predicate = r => r.Tags.Contains("ready")
}).AllowAnonymous();

// ===== Авто-миграция + сид на старте =====
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.MigrateAsync();

    if (!await db.Products.AnyAsync())
    {
        db.Products.AddRange(
            new Product { Id = Guid.NewGuid(), Name = "Keyboard", Price = 89.99m, Stock = 20 },
            new Product { Id = Guid.NewGuid(), Name = "Mouse",    Price = 39.99m, Stock = 50 }
        );
        await db.SaveChangesAsync();
    }
}

app.Run();
