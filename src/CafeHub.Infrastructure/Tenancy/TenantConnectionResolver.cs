using CafeHub.Application.Tenancy;

namespace CafeHub.Infrastructure.Tenancy
{
    internal sealed class TenantConnectionResolver(ISecretReferenceResolver secretReferenceResolver) : ITenantConnectionResolver
    {
        public string Resolve(TenantContext tenant)
        {
            if (tenant is null)
            {
                throw new InvalidTenantContextException("No tenant context was supplied to resolve a tenant connection.");
            }

            if (!tenant.IsActive)
            {
                throw new TenantInactiveException($"Tenant {tenant.TenantId} is not active. Activate the tenant before resolving its connection.");
            }

            return secretReferenceResolver.Resolve(tenant.SecretReference);
        }
    }
}
