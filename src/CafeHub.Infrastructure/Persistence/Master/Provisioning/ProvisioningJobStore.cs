using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Domain.Provisioning;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master.Provisioning
{
    internal sealed class ProvisioningJobStore(MasterDbContext context) : IProvisioningJobStore
    {
        public Task AddAsync(ProvisioningJob job, CancellationToken cancellationToken)
        {
            context.ProvisioningJobs.Add(job);
            return Task.CompletedTask;
        }

        public async Task<ProvisioningJob?> FindByIdAsync(
            Guid jobId,
            CancellationToken cancellationToken)
        {
            return await context.ProvisioningJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(job => job.Id == jobId, cancellationToken);
        }

        public async Task<ProvisioningJob?> FindByTenantIdAsync(
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            return await context.ProvisioningJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(job => job.TenantId == tenantId, cancellationToken);
        }

        public Task UpdateAsync(ProvisioningJob job, CancellationToken cancellationToken)
        {
            context.ProvisioningJobs.Update(job);
            return Task.CompletedTask;
        }
    }
}
