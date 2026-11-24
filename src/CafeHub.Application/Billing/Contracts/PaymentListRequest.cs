using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Contracts
{
    public sealed record PaymentListRequest(
        int Page,
        int PageSize,
        bool IncludeTotalCount,
        Guid? BranchId,
        Guid? BillId,
        PaymentStatus? Status);
}
