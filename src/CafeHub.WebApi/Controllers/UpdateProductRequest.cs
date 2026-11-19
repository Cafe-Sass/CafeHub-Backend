using System.Text.Json.Serialization;
using CafeHub.Domain.Tenants;

namespace CafeHub.WebApi.Controllers
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record UpdateProductRequest(
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
        [property: JsonRequired] IReadOnlyList<ProductTagInput> Tags,
        [property: JsonRequired] string ConcurrencyToken);
}
