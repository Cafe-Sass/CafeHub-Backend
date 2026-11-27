using CafeHub.Application.Refunds.DTOs;

namespace CafeHub.Application.Refunds.Contracts
{
    public sealed record RefundRequestListPage(
        IReadOnlyList<RefundRequestListItem> Entries,
        int? TotalCount);
}
