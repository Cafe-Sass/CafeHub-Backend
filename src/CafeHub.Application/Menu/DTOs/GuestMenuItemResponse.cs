namespace CafeHub.Application.Menu.DTOs
{
    /// <summary>
    /// One orderable guest menu item. Category fields let the client group the paged items; station, status,
    /// availability reason, and audit data are deliberately absent.
    /// </summary>
    public sealed record GuestMenuItemResponse(
        Guid Id,
        string Name,
        string? Description,
        string Kind,
        bool Caffeine,
        int? PreparationMinutes,
        Guid CategoryId,
        string CategoryName,
        Guid? ParentCategoryId,
        string? ParentCategoryName,
        IReadOnlyList<GuestMenuSizeResponse> Sizes,
        IReadOnlyList<GuestMenuAddonResponse> Addons,
        IReadOnlyList<string> Tags);
}
