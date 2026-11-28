using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Discovery
{
    public interface IProductDiscoveryProvider
    {
        Task<Result<IReadOnlyList<ProductDiscoverySuggestion>>> SuggestAsync(
            ProductDiscoveryInput input,
            CancellationToken cancellationToken);
    }
}
