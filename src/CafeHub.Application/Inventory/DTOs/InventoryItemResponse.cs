using System.Globalization;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Inventory.DTOs
{
    public sealed record InventoryItemResponse(
        Guid Id,
        string Name,
        string Unit,
        decimal LowStockThreshold,
        bool TracksExpiry,
        int NearExpiryDays,
        string Status,
        string ConcurrencyToken)
    {
        public static InventoryItemResponse From(InventoryItem item, uint version) =>
            new(
                item.Id,
                item.Name,
                item.Unit.ToString(),
                item.LowStockThreshold,
                item.TracksExpiry,
                item.NearExpiryDays,
                item.Status.ToString(),
                version.ToString(CultureInfo.InvariantCulture));
    }
}
