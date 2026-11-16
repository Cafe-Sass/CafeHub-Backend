using System.Text.Json.Serialization;

namespace CafeHub.WebApi.Controllers
{
    public sealed record UpdateMenuCategoryRequest(
        string Name, [property: JsonRequired] int SortOrder, string ConcurrencyToken);
}
