using System.Text.Json.Serialization;

namespace CafeHub.WebApi.Controllers
{
    public sealed record CreateMenuCategoryRequest(
        string Name, [property: JsonRequired] int SortOrder, Guid? ParentId = null);
}
