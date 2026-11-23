using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Contracts
{
    public sealed record BillListRequest(
        int Page,
        int PageSize,
        bool IncludeTotalCount,
        Guid? BranchId,
        Guid? TableId,
        BillStatus? Status);
}
