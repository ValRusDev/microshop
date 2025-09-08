using MassTransit;
using MicroShop.Contracts;
using Microsoft.Extensions.Logging;

namespace MicroShop.Services.Ordering.Application;

public class CheckoutRequestedConsumer(ILogger<CheckoutRequestedConsumer> logger) : IConsumer<CheckoutRequested>
{
    public Task Consume(ConsumeContext<CheckoutRequested> ctx)
    {
        logger.LogInformation("Ordering received CheckoutRequested: User {UserId}, Items {Count}, Total {Total}",
            ctx.Message.UserId, ctx.Message.Items.Count, ctx.Message.Total);
        // TODO: создать черновик заказа и т.д.
        return Task.CompletedTask;
    }
}
