using CafeHub.Application.Tenancy;

namespace CafeHub.Infrastructure.Tenancy
{
    public sealed class TenantContextAccessor : ITenantContextAccessor, ITenantContextSetter
    {
        private TenantContext? _tenantContext;

        public TenantContext GetRequired()
        {
            if (_tenantContext is null)
            {
                throw new TenantContextMissingException("No tenant context is set. Resolve the tenant before requesting tenant-scoped resources.");
            }

            return _tenantContext;
        }

        public void Set(TenantContext tenantContext)
        {
            ArgumentNullException.ThrowIfNull(tenantContext);

            if (_tenantContext is not null)
            {
                throw new InvalidTenantContextException("Tenant context is write-once per request and has already been set.");
            }

            _tenantContext = tenantContext;
        }
    }
}
