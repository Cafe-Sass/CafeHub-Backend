using CafeHub.Application.Menu.DTOs;

namespace CafeHub.Application.Discovery
{
    public static class ProductDiscoveryGrounding
    {
        public const int MaximumResults = 5;

        public const int MaximumReasonLength = 200;

        public static IReadOnlyList<ProductDiscoveryItem> Ground(
            IReadOnlyList<GuestMenuProductSummary> visibleProducts,
            IReadOnlyList<ProductDiscoverySuggestion>? suggestions)
        {
            ArgumentNullException.ThrowIfNull(visibleProducts);

            if (suggestions is null || suggestions.Count == 0 || visibleProducts.Count == 0)
            {
                return Array.Empty<ProductDiscoveryItem>();
            }

            var productsById = new Dictionary<Guid, GuestMenuProductSummary>();
            foreach (var product in visibleProducts)
            {
                productsById.TryAdd(product.Id, product);
            }

            var items = new List<ProductDiscoveryItem>(MaximumResults);
            var seenProductIds = new HashSet<Guid>();
            foreach (var suggestion in suggestions)
            {
                if (items.Count == MaximumResults)
                {
                    break;
                }

                if (suggestion is null
                    || !seenProductIds.Add(suggestion.ProductId)
                    || !productsById.TryGetValue(suggestion.ProductId, out var product))
                {
                    continue;
                }

                items.Add(new ProductDiscoveryItem(
                    product.Id,
                    product.Name,
                    product.PriceFrom,
                    product.PriceTo,
                    NormalizeReason(suggestion.Reason)));
            }

            return items;
        }

        public static IReadOnlyList<ProductDiscoveryItem> ToItems(
            IReadOnlyList<GuestMenuProductSummary> products)
        {
            ArgumentNullException.ThrowIfNull(products);

            return products
                .Take(MaximumResults)
                .Select(product => new ProductDiscoveryItem(
                    product.Id,
                    product.Name,
                    product.PriceFrom,
                    product.PriceTo,
                    null))
                .ToArray();
        }

        private static string? NormalizeReason(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return null;
            }

            var trimmed = reason.Trim();
            return trimmed.Length <= MaximumReasonLength
                ? trimmed
                : trimmed[..MaximumReasonLength];
        }
    }
}
