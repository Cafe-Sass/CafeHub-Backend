using CafeHub.Domain.Tenants;

namespace CafeHub.Infrastructure.Menu
{
    internal static class MenuCategoryVisibility
    {
        internal static IQueryable<MenuCategory> EffectivelyVisible(this IQueryable<MenuCategory> categories) =>
            categories.Where(category => category.Status == MenuCategoryStatus.Active
                && (category.ParentId == null || category.Parent!.Status == MenuCategoryStatus.Active));
    }
}
