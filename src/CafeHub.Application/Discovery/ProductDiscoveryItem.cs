namespace CafeHub.Application.Discovery
{
    public sealed record ProductDiscoveryItem(
        Guid Id,
        string Name,
        decimal? PriceFrom,
        decimal? PriceTo,
        string? Reason);
}
