namespace CafeHub.WebApi.Controllers
{
    public sealed record AssistedOrderResponse(
        int OrderNumber,
        string PublicReference,
        string Status,
        decimal TotalAmount);
}
