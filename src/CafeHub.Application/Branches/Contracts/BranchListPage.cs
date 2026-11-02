using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Branches.Contracts
{
    /// <summary>
    /// One branches list page as read from persistence. <see cref="FetchedItems"/> contains at most the requested
    /// page size plus one row; the extra row signals a following page and is trimmed by the query handler.
    /// <see cref="TotalCount"/> is populated only when the request asked for it.
    /// </summary>
    public sealed record BranchListPage(IReadOnlyList<BranchResponse> FetchedItems, int? TotalCount);
}
