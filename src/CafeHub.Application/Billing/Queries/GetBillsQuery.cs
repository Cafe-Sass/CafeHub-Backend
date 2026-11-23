using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Queries
{
    public sealed record GetBillsQuery : IQuery<OffsetPage<BillListItem>>
    {
        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public Guid? BranchId { get; init; }

        public Guid? TableId { get; init; }

        public BillStatus? Status { get; init; }
    }
}
