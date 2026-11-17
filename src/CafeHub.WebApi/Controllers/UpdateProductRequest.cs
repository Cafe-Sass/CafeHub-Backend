using System.Text.Json.Serialization;

namespace CafeHub.WebApi.Controllers
{
    public sealed record UpdateProductRequest(
        Guid CategoryId,
        Guid? StationId,
        string Name,
        string? Description,
        string? Ingredients,
        [property: JsonRequired] string Kind,
        bool Caffeine,
        int? PreparationMinutes,
        [property: JsonRequired] int SortOrder,
        IReadOnlyList<CreateProductSizeRequest> Sizes,
        IReadOnlyList<CreateProductAddonRequest> Addons,
        IReadOnlyList<string> Tags,
        [property: JsonRequired] string ConcurrencyToken);
}
