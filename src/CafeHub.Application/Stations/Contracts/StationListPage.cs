using CafeHub.Application.Stations.DTOs;

namespace CafeHub.Application.Stations.Contracts
{
    /// <summary>
    /// One stations list page as read from persistence. <see cref="FetchedItems"/> contains at most the requested
    /// page size plus one row; the extra row signals a following page and is trimmed by the query handler.
    /// <see cref="TotalCount"/> is populated only when the request asked for it.
    /// </summary>
    public sealed record StationListPage(IReadOnlyList<StationResponse> FetchedItems, int? TotalCount);
}
