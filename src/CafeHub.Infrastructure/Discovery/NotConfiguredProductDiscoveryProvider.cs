using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Discovery;

namespace CafeHub.Infrastructure.Discovery
{
    public sealed class NotConfiguredProductDiscoveryProvider : IProductDiscoveryProvider
    {
        public Task<Result<IReadOnlyList<ProductDiscoverySuggestion>>> SuggestAsync(
            ProductDiscoveryInput input,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(input);

            return Task.FromResult(
                Result.Success<IReadOnlyList<ProductDiscoverySuggestion>>(Array.Empty<ProductDiscoverySuggestion>()));
        }
    }
}
