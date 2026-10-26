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
    }
}
