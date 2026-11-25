namespace CafeHub.WebApi.Controllers
{
    public sealed record CreateInventoryItemRequest(
        string? Name,
        string? Unit,
        decimal LowStockThreshold,
        bool TracksExpiry,
        int? NearExpiryDays);
}
