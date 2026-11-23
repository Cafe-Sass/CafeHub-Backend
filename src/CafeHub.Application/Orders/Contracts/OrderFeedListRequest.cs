using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Orders.Contracts
{
    public sealed record OrderFeedListRequest(
        int Page,
        int PageSize,
        bool IncludeTotalCount,
        Guid BranchId,
        OrderStatus? Status,
        DateTimeOffset? UpdatedSince,
        IReadOnlyList<Guid>? StationIds,
        bool IncludeMuted);
}
