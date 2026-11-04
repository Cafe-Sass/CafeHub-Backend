using CafeHub.Application.Upgrades.Contracts;
using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master.Upgrades
{
    internal sealed class TenantSchemaUpgradeRunStore(MasterDbContext context) : ITenantSchemaUpgradeRunStore
    {
        public async Task<TenantSchemaUpgradeRun?> FindByIdAsync(
            Guid runId,
            CancellationToken cancellationToken)
        {
            return await context.TenantSchemaUpgradeRuns
                .FirstOrDefaultAsync(run => run.Id == runId, cancellationToken);
        }

        public async Task<TenantSchemaUpgradeRun?> FindActiveByTenantIdAsync(
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            return await context.TenantSchemaUpgradeRuns
                .FirstOrDefaultAsync(
                    run => run.TenantId == tenantId &&
                           (run.Status == TenantSchemaUpgradeRunStatus.Pending ||
                            run.Status == TenantSchemaUpgradeRunStatus.Running),
                    cancellationToken);
        }

        public Task AddAsync(TenantSchemaUpgradeRun run, CancellationToken cancellationToken)
        {
            context.TenantSchemaUpgradeRuns.Add(run);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TenantSchemaUpgradeRun run, CancellationToken cancellationToken)
        {
            context.TenantSchemaUpgradeRuns.Update(run);
            return Task.CompletedTask;
        }
    }
}
