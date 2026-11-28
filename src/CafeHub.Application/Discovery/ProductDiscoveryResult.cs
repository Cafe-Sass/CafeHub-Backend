namespace CafeHub.Application.Discovery
{
    public sealed record ProductDiscoveryResult(
        string Source,
        IReadOnlyList<ProductDiscoveryItem> Items);
}
