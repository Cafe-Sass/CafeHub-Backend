using CafeHub.Application.Inventory.DTOs;

namespace CafeHub.Application.Inventory.Contracts
{
    public sealed record InventoryStockLevelRequest(
        Guid? BranchId,
        Guid? ItemId,
        int Page,
        int PageSize,
        bool IncludeTotalCount);

    public sealed record InventoryStockLevelPage(IReadOnlyList<StockLevelResponse> Items, int? TotalCount);
}
