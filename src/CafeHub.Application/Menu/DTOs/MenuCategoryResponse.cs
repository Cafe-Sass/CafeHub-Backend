namespace CafeHub.Application.Menu.DTOs
{
    public record MenuCategoryResponse(
        Guid Id, Guid? ParentId, string Name, int SortOrder, string Status, string ConcurrencyToken);
}
