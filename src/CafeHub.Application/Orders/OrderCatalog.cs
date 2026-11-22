using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Orders
{
    public sealed record OrderCatalog(IReadOnlyList<OrderCatalogProduct> Products)
    {
        public OrderCatalogProduct? FindProduct(Guid productId) =>
            Products.FirstOrDefault(product => product.Id == productId);
    }

    public sealed record OrderCatalogProduct(
        Guid Id,
        string Name,
        Guid? StationId,
        ProductStatus Status,
        bool IsVisible,
        bool IsAvailableInBranch,
        IReadOnlyList<OrderCatalogSize> Sizes,
        IReadOnlyList<OrderCatalogAddon> Addons)
    {
        public OrderCatalogSize? FindSize(Guid sizeId) =>
            Sizes.FirstOrDefault(size => size.Id == sizeId);

        public OrderCatalogAddon? FindAddon(Guid addonId) =>
            Addons.FirstOrDefault(addon => addon.Id == addonId);
    }

    public sealed record OrderCatalogSize(Guid Id, string Name, decimal Price);

    public sealed record OrderCatalogAddon(Guid Id, string Name, decimal Price);
}
