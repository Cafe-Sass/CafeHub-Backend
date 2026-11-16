using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Tables
{
    internal static class TableActor
    {
        internal static bool TryGetActorUserId(
            ICurrentTenantUser currentTenantUser,
            TenantContext tenantContext,
            out Guid userId)
        {
            var candidate = currentTenantUser.UserId;
            userId = candidate ?? Guid.Empty;
            return currentTenantUser.IsAuthenticated
                && candidate is { } value
                && value != Guid.Empty
                && currentTenantUser.TenantId is not null
                && currentTenantUser.TenantId == tenantContext.TenantId
                && tenantContext.IsActive;
        }
    }
}
