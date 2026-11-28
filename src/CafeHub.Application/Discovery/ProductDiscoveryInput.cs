namespace CafeHub.Application.Discovery
{
    public sealed record ProductDiscoveryCandidate(Guid Id, string Name);

    public sealed record ProductDiscoveryInput(
        string Text,
        IReadOnlyList<ProductDiscoveryCandidate> Products);
}
