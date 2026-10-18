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
            _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        }
    }
}
