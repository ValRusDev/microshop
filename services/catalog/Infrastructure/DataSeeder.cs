using Microsoft.EntityFrameworkCore;
using MicroShop.Services.Catalog.Infrastructure;
using MicroShop.Services.Catalog.Domain;

public sealed class DataSeeder : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(IServiceProvider sp, ILogger<DataSeeder> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        await db.Database.MigrateAsync(ct);

        if (!await db.Products.AnyAsync(ct))
        {
            db.Products.AddRange(
                new Product { Id = Guid.NewGuid(), Name = "Keyboard", Price = 89.99m, Stock = 20 },
                new Product { Id = Guid.NewGuid(), Name = "Mouse", Price = 39.99m, Stock = 50 }
            );
            await db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Catalog seeded");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
