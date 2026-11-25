using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Inventory
{
    internal static class InventoryItemActor
    {
        internal static bool TryGetId(
            ICurrentTenantUser currentUser, ITenantContextAccessor tenantContext, out Guid id)
        {
            var tenant = tenantContext.GetRequired();
            id = currentUser.UserId.GetValueOrDefault();
            return tenant.IsActive && currentUser.IsAuthenticated && id != Guid.Empty
                && currentUser.TenantId == tenant.TenantId;
        }
    }
}
