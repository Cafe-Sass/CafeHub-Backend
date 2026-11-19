namespace CafeHub.Domain.Tenants
{
    public sealed class ProductRoutingReadiness
    {
        private ProductRoutingReadiness() { }

        public bool Singleton { get; private init; }

        public DateTimeOffset VerifiedAt { get; private init; }
    }
}
