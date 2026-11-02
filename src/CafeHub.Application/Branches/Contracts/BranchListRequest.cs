using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Branches.Contracts
{
    /// <summary>
    /// Offset-page request for the branches list. <see cref="PageSize"/> is the resolved page size; the store
    /// fetches at most one extra row so the caller can derive <see cref="OffsetPage{T}.HasNextPage"/> without a count.
    /// </summary>
    public sealed record BranchListRequest(
        int Page,
        int PageSize,
        IReadOnlyList<SortTerm> Sort,
        bool IncludeTotalCount,
        BranchLifecycleStatus? Status);
}
