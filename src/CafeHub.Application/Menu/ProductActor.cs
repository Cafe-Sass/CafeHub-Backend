using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Menu
{
    internal static class ProductActor
    {
        internal static Error? CheckBranch(
            ICurrentTenantUser user, ITenantContextAccessor tenant, Guid branchId, out Guid actorId)
        {
            if (!TryGetId(user, tenant, out actorId))
            {
                return AuthErrors.Forbidden();
            }

            var scope = TenantBranchScope.Resolve(user.AccountAccess);
            if (!scope.IsSuccess)
            {
                return scope.Error;
            }

            return scope.Value is { } allowedBranch && branchId != allowedBranch
                ? ProductErrors.NotFound()
                : null;
        }

        internal static bool TryGetId(ICurrentTenantUser currentUser, ITenantContextAccessor tenantContext, out Guid id)
        {
            var tenant = tenantContext.GetRequired();
            id = currentUser.UserId.GetValueOrDefault();
            return tenant.IsActive && currentUser.IsAuthenticated && id != Guid.Empty
                && currentUser.TenantId == tenant.TenantId;
        }
    }
}
