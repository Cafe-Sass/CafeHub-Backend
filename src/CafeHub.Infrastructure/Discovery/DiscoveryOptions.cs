namespace CafeHub.Infrastructure.Discovery
{
    public sealed class DiscoveryOptions
    {
        public string Provider { get; set; } = DiscoveryProviders.None;
    }
}
