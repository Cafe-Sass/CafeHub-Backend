using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Domain.Provisioning;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master.Provisioning
{
    internal sealed class ProvisioningJobAttemptStore(MasterDbContext context) : IProvisioningJobAttemptStore
    {
        public Task AddAsync(ProvisioningJobAttempt attempt, CancellationToken cancellationToken)
        {
            context.ProvisioningJobAttempts.Add(attempt);
            return Task.CompletedTask;
        }
    }
}
