using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Menu.DTOs
{
    public sealed record ProductSizeResponse(
        Guid Id, string Name, decimal Price, bool IsDefault, string Status)
    {
        public static ProductSizeResponse From(ProductSize size)
        {
            ArgumentNullException.ThrowIfNull(size);
            return new ProductSizeResponse(size.Id, size.Name, size.Price, size.IsDefault, size.Status.ToString());
        }
    }
}
