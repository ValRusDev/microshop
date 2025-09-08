using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using MicroShop.Services.Basket.Domain;

namespace MicroShop.Services.Basket.Infrastructure;

public interface IBasketStore
{
    Task<UserBasket> GetAsync(Guid userId, CancellationToken ct = default);
    Task SaveAsync(UserBasket basket, CancellationToken ct = default);
    Task ClearAsync(Guid userId, CancellationToken ct = default);
}

public class RedisBasketStore(IDistributedCache cache) : IBasketStore
{
    private static string Key(Guid userId) => $"basket:{userId}";

    public async Task<UserBasket> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var bytes = await cache.GetAsync(Key(userId), ct);
        if (bytes is null) return UserBasket.Empty(userId);
        var basket = JsonSerializer.Deserialize<UserBasket>(bytes);
        return basket ?? UserBasket.Empty(userId);
    }

    public async Task SaveAsync(UserBasket basket, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(basket);
        var opts = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) };
        await cache.SetAsync(Key(basket.UserId), bytes, opts, ct);
    }

    public Task ClearAsync(Guid userId, CancellationToken ct = default) =>
        cache.RemoveAsync(Key(userId), ct);
}
