namespace MicroShop.Contracts;

public record BasketItemDto(Guid ProductId, int Quantity, decimal UnitPrice);
public record CheckoutRequested(Guid UserId, IReadOnlyList<BasketItemDto> Items, decimal Total);
