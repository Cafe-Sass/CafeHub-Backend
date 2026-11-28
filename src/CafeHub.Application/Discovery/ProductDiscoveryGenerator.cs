using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using Microsoft.Extensions.Logging;

namespace CafeHub.Application.Discovery
{
    public sealed class ProductDiscoveryGenerator(
        IProductDiscoveryProvider provider,
        IGuestMenuStore menuStore,
        ILogger<ProductDiscoveryGenerator> logger)
    {
        public async Task<ProductDiscoveryResult> DiscoverAsync(
            Guid branchId,
            string text,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(text);

            var visibleProducts = await menuStore.ListVisibleAsync(branchId, cancellationToken);
            var suggestions = await TrySuggestAsync(text, visibleProducts, cancellationToken);
            var items = ProductDiscoveryGrounding.Ground(visibleProducts, suggestions);
            if (items.Count > 0)
            {
                return new ProductDiscoveryResult(ProductDiscoverySources.Ai, items);
            }

            LogFallback(suggestions is null ? "Discovery.ProviderFailure" : "Discovery.NoValidProducts");
            return await SearchAsync(branchId, text, cancellationToken);
        }

        private async Task<IReadOnlyList<ProductDiscoverySuggestion>?> TrySuggestAsync(
            string text,
            IReadOnlyList<GuestMenuProductSummary> visibleProducts,
            CancellationToken cancellationToken)
        {
            var input = new ProductDiscoveryInput(
                text,
                visibleProducts
                    .Select(product => new ProductDiscoveryCandidate(product.Id, product.Name))
                    .ToArray());

            try
            {
                var result = await provider.SuggestAsync(input, cancellationToken);
                return result.IsSuccess ? result.Value : null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                LogFallback(exception.GetType().Name);
                return null;
            }
        }

        private async Task<ProductDiscoveryResult> SearchAsync(
            Guid branchId,
            string text,
            CancellationToken cancellationToken)
        {
            var products = await menuStore.SearchVisibleAsync(
                branchId,
                text,
                ProductDiscoveryGrounding.MaximumResults,
                cancellationToken);
            return new ProductDiscoveryResult(
                ProductDiscoverySources.Search,
                ProductDiscoveryGrounding.ToItems(products));
        }

        private void LogFallback(string reason)
        {
            logger.LogWarning(
                "The discovery provider did not return usable products ({Reason}); the guest menu search is used.",
                reason);
        }
    }
}
