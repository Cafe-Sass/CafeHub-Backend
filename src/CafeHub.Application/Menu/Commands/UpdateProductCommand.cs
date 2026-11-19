using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Menu.Commands
{
    public sealed record UpdateProductCommand(
        Guid Id,
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
        IReadOnlyList<ProductTagInput> Tags,
        string ConcurrencyToken) : ICommand<ProductResponse>, ITenantTransactionalCommand
    {
        internal ProductWriteModel ToWriteModel() => new(
            CategoryId, StationId, Name, Description, Ingredients, Kind, Caffeine,
            PreparationMinutes, SortOrder, Sizes, Addons, Tags);
    }
}
