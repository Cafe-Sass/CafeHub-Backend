using CafeHub.Application.Inventory.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Inventory.Contracts
{
    public sealed record InventoryItemListRequest(
        InventoryItemStatus? Status,
        int Page,
        int PageSize,
        bool IncludeTotalCount);

    public sealed record InventoryItemListPage(IReadOnlyList<InventoryItemResponse> Items, int? TotalCount);
}
