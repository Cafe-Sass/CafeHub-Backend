using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Stations.Contracts
{
    /// <summary>
    /// Offset-page request for one branch's station list. <see cref="PageSize"/> is the resolved page size; the
    /// store fetches at most one extra row so the caller can derive <c>HasNextPage</c> without a count.
    /// </summary>
    public sealed record StationListRequest(
        Guid BranchId,
        int Page,
        int PageSize,
        bool IncludeTotalCount,
        StationStatus? Status);
}
