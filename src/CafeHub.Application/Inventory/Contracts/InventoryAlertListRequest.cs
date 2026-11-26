using CafeHub.Application.Inventory.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Inventory.Contracts
{
    public sealed record InventoryAlertListRequest(
        Guid? BranchId,
        InventoryAlertKind? Kind,
        InventoryAlertState? State,
        int Page,
        int PageSize,
        bool IncludeTotalCount);

    public sealed record InventoryAlertListPage(
        IReadOnlyList<InventoryAlertResponse> Items, int? TotalCount);
}
