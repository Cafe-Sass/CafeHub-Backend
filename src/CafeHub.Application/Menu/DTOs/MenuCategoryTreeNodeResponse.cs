namespace CafeHub.Application.Menu.DTOs
{
    public sealed record MenuCategoryTreeNodeResponse(
        Guid Id, Guid? ParentId, string Name, int SortOrder, string Status, string ConcurrencyToken,
        IReadOnlyList<MenuCategoryResponse> Children)
        : MenuCategoryResponse(Id, ParentId, Name, SortOrder, Status, ConcurrencyToken);
}
