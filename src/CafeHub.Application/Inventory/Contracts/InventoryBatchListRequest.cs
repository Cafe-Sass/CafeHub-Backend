using CafeHub.Application.Inventory.DTOs;

namespace CafeHub.Application.Inventory.Contracts
{
    public sealed record InventoryBatchListRequest(
        Guid BranchId,
        Guid? ItemId,
        DateOnly? ExpiryFrom,
        DateOnly? ExpiryTo,
        int Page,
        int PageSize,
        bool IncludeTotalCount);

    public sealed record InventoryBatchListPage(IReadOnlyList<InventoryBatchResponse> Items, int? TotalCount);
}
