using CafeHub.Application.Tenancy;
using CafeHub.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Tenancy
{
    internal sealed class TenantRegistryReader(MasterDbContext context) : ITenantRegistryReader
    {
        public Task<TenantRegistration?> FindBySlugAsync(string normalizedSlug, CancellationToken cancellationToken)
        {
            return FindAsync(context.Tenants.Where(tenant => tenant.Slug == normalizedSlug), cancellationToken);
        }

        public Task<TenantRegistration?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            return FindAsync(context.Tenants.Where(tenant => tenant.Id == tenantId), cancellationToken);
        }

        private Task<TenantRegistration?> FindAsync(
            IQueryable<global::CafeHub.Domain.Tenants.Tenant> tenants,
            CancellationToken cancellationToken)
        {
            return tenants
                .AsNoTracking()
                .Select(tenant => new TenantRegistration(
                    tenant.Id,
                    tenant.Slug,
                    tenant.LifecycleStatus,
                    context.TenantDatabases
                        .Where(database => database.TenantId == tenant.Id)
                        .Select(database => (string?)database.SecretReference)
                        .FirstOrDefault()))
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
