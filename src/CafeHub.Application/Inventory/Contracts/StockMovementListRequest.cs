using CafeHub.Application.Inventory.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Inventory.Contracts
{
    public sealed record StockMovementListRequest(
        Guid? BranchId,
        Guid? ItemId,
        StockMovementType? Type,
        DateOnly? CreatedFrom,
        DateOnly? CreatedTo,
        int Page,
        int PageSize,
        bool IncludeTotalCount);

    public sealed record StockMovementListPage(IReadOnlyList<StockMovementResponse> Items, int? TotalCount);
}
