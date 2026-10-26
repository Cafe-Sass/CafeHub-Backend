using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master.Onboarding
{
    internal sealed class TenantDatabaseStore(MasterDbContext context) : ITenantDatabaseStore
    {
        public async Task<TenantDatabase?> FindByTenantIdAsync(
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            return await context.TenantDatabases
                .AsNoTracking()
                .FirstOrDefaultAsync(database => database.TenantId == tenantId, cancellationToken);
        }

        public Task AddAsync(
            TenantDatabase tenantDatabase,
            CancellationToken cancellationToken)
        {
            context.TenantDatabases.Add(tenantDatabase);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TenantDatabase tenantDatabase, CancellationToken cancellationToken)
        {
            context.TenantDatabases.Update(tenantDatabase);
            return Task.CompletedTask;
        }
    }
}
