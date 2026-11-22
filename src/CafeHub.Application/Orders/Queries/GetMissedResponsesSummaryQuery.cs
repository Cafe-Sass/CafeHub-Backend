using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Orders.Queries
{
    public sealed record GetMissedResponsesSummaryQuery : IQuery<OffsetPage<MissedResponseSummaryEntry>>
    {
        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public Guid? BranchId { get; init; }

        public DateOnly From { get; init; }

        public DateOnly To { get; init; }
    }
}
