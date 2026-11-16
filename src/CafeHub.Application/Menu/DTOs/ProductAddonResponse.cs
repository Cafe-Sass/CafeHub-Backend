using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Menu.DTOs
{
    public sealed record ProductAddonResponse(Guid Id, string Name, decimal Price, string Status)
    {
        public static ProductAddonResponse From(ProductAddon addon)
        {
            ArgumentNullException.ThrowIfNull(addon);
            return new ProductAddonResponse(addon.Id, addon.Name, addon.Price, addon.Status.ToString());
        }
    }
}
