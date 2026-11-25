namespace CafeHub.WebApi.Controllers
{
    public sealed record ReceiveStockRequest(
        Guid ItemId,
        decimal ReceivedQuantity,
        DateOnly ReceivedDate,
        DateOnly? ExpiryDate);
}
