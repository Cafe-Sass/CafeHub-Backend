using System.Text.Json.Serialization;
using CafeHub.Domain.Tenants;

namespace CafeHub.WebApi.Controllers
{
    public sealed record CreateProductSizeRequest(Guid? Id, string Name, decimal Price, bool IsDefault);

    public sealed record CreateProductAddonRequest(Guid? Id, string Name, decimal Price);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record CreateProductRequest(
        Guid CategoryId,
        string Name,
        string? Description,
        string? Ingredients,
        [property: JsonRequired] string Kind,
        bool Caffeine,
        int? PreparationMinutes,
        [property: JsonRequired] int SortOrder,
        IReadOnlyList<CreateProductSizeRequest> Sizes,
        IReadOnlyList<CreateProductAddonRequest> Addons,
        [property: JsonRequired] IReadOnlyList<ProductTagInput> Tags);
}
