namespace CafeHub.WebApi.Controllers
{
    public sealed record UpdateInventoryItemRequest(
        string? Name,
        string? Unit,
        decimal LowStockThreshold,
        bool TracksExpiry,
        int NearExpiryDays,
        string ConcurrencyToken);
}
