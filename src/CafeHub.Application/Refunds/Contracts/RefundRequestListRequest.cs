using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Refunds.Contracts
{
    public sealed record RefundRequestListRequest(
        int Page,
        int PageSize,
        bool IncludeTotalCount,
        Guid? BranchId,
        RefundRequestStatus? Status,
        Guid? AssignedUserId,
        Guid? BillId);
}
