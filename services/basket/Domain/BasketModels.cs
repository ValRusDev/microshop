namespace MicroShop.Services.Basket.Domain;

public record BasketItem(Guid ProductId, int Quantity, decimal UnitPrice);
public record UserBasket(Guid UserId, List<BasketItem> Items)
{
    public decimal Total => Items.Sum(i => i.UnitPrice * i.Quantity);
    public static UserBasket Empty(Guid userId) => new(userId, new());
}
