using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Menu.DTOs
{
    public sealed class ProductResponse
    {
        public required Guid Id { get; init; }

        public required Guid CategoryId { get; init; }

        public required Guid? StationId { get; init; }

        public required string Name { get; init; }

        public required string? Description { get; init; }

        public required string? Ingredients { get; init; }

        public required string Kind { get; init; }

        public required bool Caffeine { get; init; }

        public required int? PreparationMinutes { get; init; }

        public required int SortOrder { get; init; }

        public required string Status { get; init; }

        public required IReadOnlyList<ProductSizeResponse> Sizes { get; init; }

        public required IReadOnlyList<ProductAddonResponse> Addons { get; init; }

        public required IReadOnlyList<string> Tags { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }

        public required DateTimeOffset UpdatedAt { get; init; }

        public required string ConcurrencyToken { get; init; }

        public static ProductResponse From(Product product, string concurrencyToken)
        {
            ArgumentNullException.ThrowIfNull(product);
            ArgumentNullException.ThrowIfNull(concurrencyToken);

            return new ProductResponse
            {
                Id = product.Id,
                CategoryId = product.CategoryId,
                StationId = product.StationId,
                Name = product.Name,
                Description = product.Description,
                Ingredients = product.Ingredients,
                Kind = product.Kind.ToString(),
                Caffeine = product.Caffeine,
                PreparationMinutes = product.PreparationMinutes,
                SortOrder = product.SortOrder,
                Status = product.Status.ToString(),
                Sizes = product.Sizes.Select(size => ProductSizeResponse.From(size)).ToArray(),
                Addons = product.Addons.Select(addon => ProductAddonResponse.From(addon)).ToArray(),
                Tags = product.Tags.Select(tag => tag.Tag).OrderBy(tag => tag, StringComparer.Ordinal).ToArray(),
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                ConcurrencyToken = concurrencyToken,
            };
        }
    }
}
