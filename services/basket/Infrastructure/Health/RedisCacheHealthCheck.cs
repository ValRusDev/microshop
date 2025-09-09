using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MicroShop.Services.Basket.Infrastructure.Health;

public class RedisCacheHealthCheck(IDistributedCache cache) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var key = "hc:" + Guid.NewGuid().ToString("N");
            await cache.SetStringAsync(key, "1",
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5) }, ct);
            var val = await cache.GetStringAsync(key, ct);
            return val == "1" ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Redis roundtrip failed");
        }
        catch (Exception ex) { return HealthCheckResult.Unhealthy("Redis error", ex); }
    }
}
