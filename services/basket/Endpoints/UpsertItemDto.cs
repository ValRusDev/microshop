namespace MicroShop.Services.Basket.Endpoints
{
    public record UpsertItemDto(Guid ProductId, int Quantity, decimal UnitPrice);
}
