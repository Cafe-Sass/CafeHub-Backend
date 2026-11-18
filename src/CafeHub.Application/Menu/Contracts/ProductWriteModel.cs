using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Menu.Contracts
{
    public sealed record ProductWriteModel(
        Guid CategoryId,
        Guid? StationId,
        string Name,
        string? Description,
        string? Ingredients,
        ProductKind Kind,
        bool Caffeine,
        int? PreparationMinutes,
        int SortOrder,
        IReadOnlyList<ProductSizeInput> Sizes,
        IReadOnlyList<ProductAddonInput> Addons,
        IReadOnlyList<ProductTagInput> Tags);
}
