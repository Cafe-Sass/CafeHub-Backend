using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Menu.DTOs
{
    /// <summary>
    /// Compact product list item. See <see cref="ProductResponse"/> for the full product including sizes, add-ons,
    /// tags, and station.
    /// </summary>
    public sealed record ProductListItemResponse(
        Guid Id,
        string Name,
        string Kind,
        bool Caffeine,
        Guid CategoryId,
        decimal? DefaultSizePrice,
        string Status)
    {
        public static ProductListItemResponse From(CafeHub.Domain.Tenants.Product product, decimal? defaultSizePrice)
        {
            return new ProductListItemResponse(
                product.Id,
                product.Name,
                product.Kind.ToString(),
                product.Caffeine,
                product.CategoryId,
                defaultSizePrice,
                product.Status.ToString());
        }
    }
}
