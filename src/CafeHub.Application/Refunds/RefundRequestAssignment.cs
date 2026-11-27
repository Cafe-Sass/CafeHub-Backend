using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Refunds
{
    public static class RefundRequestAssignment
    {
        public static bool IsAllowed(TenantUserKind userKind, Guid? userBranchId, Guid requestBranchId) =>
            userKind == TenantUserKind.Owner || userBranchId == requestBranchId;
    }
}
