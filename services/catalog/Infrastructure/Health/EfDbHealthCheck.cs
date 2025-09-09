using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MicroShop.Services.Catalog.Infrastructure.Health;

public class EfDbHealthCheck<TContext> : IHealthCheck where TContext : DbContext
{
    private readonly TContext _db;
    public EfDbHealthCheck(TContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var can = await _db.Database.CanConnectAsync(ct);
            return can ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("DB not reachable");
        }
        catch (Exception ex) { return HealthCheckResult.Unhealthy("DB check failed", ex); }
    }
}
