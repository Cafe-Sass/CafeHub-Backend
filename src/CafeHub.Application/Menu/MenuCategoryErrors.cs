using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Menu
{
    public static class MenuCategoryErrors
    {
        public const string InvalidParentMessage = "The parent must be an active root category.";

        public static Error InvalidParent() => ValidationError.Create([new("parentId", InvalidParentMessage)]);

        public static Error NotFound() => Error.NotFound("MenuCategories.NotFound", "The menu category was not found.");

        public static Error NameConflict() =>
            Error.Conflict("MenuCategories.NameConflict", "An active sibling category already uses this name.");

        public static Error ConcurrencyConflict() =>
            Error.Conflict("MenuCategories.ConcurrencyConflict", "The menu category was modified. Refresh and try again.");

        public static Error ParentInactive() =>
            Error.Conflict("MenuCategories.ParentInactive", "The parent category must be active before reactivating this category.");
    }
}
