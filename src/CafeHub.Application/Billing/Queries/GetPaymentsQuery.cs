using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Queries
{
    public sealed record GetPaymentsQuery : IQuery<OffsetPage<PaymentListItem>>
    {
        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public Guid? BranchId { get; init; }

        public Guid? BillId { get; init; }

        public PaymentStatus? Status { get; init; }
    }
}
