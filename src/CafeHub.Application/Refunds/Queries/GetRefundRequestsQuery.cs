using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Pagination;
using CafeHub.Application.Refunds.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Refunds.Queries
{
    public sealed record GetRefundRequestsQuery : IQuery<OffsetPage<RefundRequestListItem>>
    {
        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public Guid? BranchId { get; init; }

        public RefundRequestStatus? Status { get; init; }

        public Guid? AssignedUserId { get; init; }

        public Guid? BillId { get; init; }
    }
}
