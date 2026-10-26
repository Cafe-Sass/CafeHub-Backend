using System.Collections.Generic;
using System.Linq;
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

        public async Task<IReadOnlyList<ProvisioningJobAttempt>> FindByJobIdAsync(
            Guid jobId,
            CancellationToken cancellationToken)
        {
            return await context.ProvisioningJobAttempts
                .AsNoTracking()
                .Where(attempt => attempt.ProvisioningJobId == jobId)
                .ToListAsync(cancellationToken);
        }
    }
}
