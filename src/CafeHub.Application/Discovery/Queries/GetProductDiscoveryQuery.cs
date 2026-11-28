using CafeHub.Application.Abstractions.Messaging;

namespace CafeHub.Application.Discovery.Queries
{
    public sealed record GetProductDiscoveryQuery : IQuery<ProductDiscoveryResult>, ISensitiveRequest
    {
        public string? Code { get; init; }

        public string? Query { get; init; }
    }
}
