using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Onboarding.Contracts
{
    /// <summary>
    /// Persistence port for tenant database references, implemented in Infrastructure against the Master database.
    /// </summary>
    public interface ITenantDatabaseStore
    {
        Task<TenantDatabase?> FindByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken);

        Task AddAsync(TenantDatabase tenantDatabase, CancellationToken cancellationToken);

        Task UpdateAsync(TenantDatabase tenantDatabase, CancellationToken cancellationToken);
    }
}
