using MassTransit;
using MicroShop.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroShop.Services.Ordering.Domain;
using MicroShop.Services.Ordering.Infrastructure;

namespace MicroShop.Services.Ordering.Application;

public class CheckoutRequestedConsumer(ILogger<CheckoutRequestedConsumer> logger, OrderingDbContext db)
    : IConsumer<CheckoutRequested>
{
    public async Task Consume(ConsumeContext<CheckoutRequested> ctx)
    {
        var msg = ctx.Message;

        // простая идемпотентность по CorrelationId
        var existing = await db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == ctx.CorrelationId);
        if (existing is not null) return;

        var order = new Order
        {
            Id = ctx.CorrelationId ?? Guid.NewGuid(),
            UserId = msg.UserId,
            Total = msg.Total,
            Status = "Pending",
            Items = msg.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        db.Add(order);
        await db.SaveChangesAsync();
        logger.LogInformation("Order {OrderId} saved ({ItemCount} items).", order.Id, order.Items.Count);
    }
}
