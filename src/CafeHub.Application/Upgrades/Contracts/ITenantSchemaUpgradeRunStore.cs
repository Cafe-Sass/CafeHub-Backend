using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Upgrades.Contracts
{
    public interface ITenantSchemaUpgradeRunStore
    {
        Task<TenantSchemaUpgradeRun?> FindByIdAsync(Guid runId, CancellationToken cancellationToken);

        Task<TenantSchemaUpgradeRun?> FindActiveByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken);

        Task AddAsync(TenantSchemaUpgradeRun run, CancellationToken cancellationToken);

        Task UpdateAsync(TenantSchemaUpgradeRun run, CancellationToken cancellationToken);
    }
}
