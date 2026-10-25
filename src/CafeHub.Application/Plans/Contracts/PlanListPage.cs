using CafeHub.Domain.Plans;

namespace CafeHub.Application.Plans.Contracts
{
    /// <summary>
    /// One plans catalog page as read from persistence. <see cref="FetchedItems"/> contains at most the requested
    /// page size plus one row; the extra row signals a following page and is trimmed by the query handler.
    /// <see cref="TotalCount"/> is populated only when the request asked for it.
    /// </summary>
    public sealed record PlanListPage(IReadOnlyList<Plan> FetchedItems, int? TotalCount);
}
