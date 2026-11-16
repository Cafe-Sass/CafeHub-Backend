using System.Text.Json.Serialization;

namespace CafeHub.WebApi.Controllers
{
    public sealed record CreateProductSizeRequest(Guid? Id, string Name, decimal Price, bool IsDefault);

    public sealed record CreateProductAddonRequest(Guid? Id, string Name, decimal Price);

    public sealed record CreateProductRequest(
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
        IReadOnlyList<string> Tags);
}
