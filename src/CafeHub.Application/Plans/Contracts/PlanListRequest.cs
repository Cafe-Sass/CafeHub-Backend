using CafeHub.Application.Pagination;

namespace CafeHub.Application.Plans.Contracts
{
    /// <summary>
    /// Offset-page request for the plans catalog. <see cref="PageSize"/> is the resolved page size; the repository
    /// fetches at most one extra row so the caller can derive <see cref="OffsetPage{T}.HasNextPage"/> without a count.
    /// </summary>
    public sealed record PlanListRequest(
        int Page,
        int PageSize,
        IReadOnlyList<SortTerm> Sort,
        bool IncludeTotalCount);
}
