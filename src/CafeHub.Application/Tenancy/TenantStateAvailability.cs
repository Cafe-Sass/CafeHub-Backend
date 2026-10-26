using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Tenancy
{
    /// <summary>Defines which registry lifecycle states may receive tenant-scoped requests.</summary>
    public static class TenantStateAvailability
    {
        public static bool IsUsable(TenantLifecycleStatus state)
        {
            return state switch
            {
                TenantLifecycleStatus.Provisioning => false,
                TenantLifecycleStatus.Active => true,
                TenantLifecycleStatus.Suspended => false,
                TenantLifecycleStatus.Terminated => false,
                _ => false,
            };
        }
    }
}
