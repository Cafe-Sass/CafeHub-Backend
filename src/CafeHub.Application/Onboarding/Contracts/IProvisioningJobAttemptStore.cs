using System.Collections.Generic;
using CafeHub.Domain.Provisioning;

namespace CafeHub.Application.Onboarding.Contracts
{
    /// <summary>
    /// Persistence port for provisioning job attempts, implemented in Infrastructure against the Master database.
    /// </summary>
    public interface IProvisioningJobAttemptStore
    {
        Task AddAsync(ProvisioningJobAttempt attempt, CancellationToken cancellationToken);

        Task<IReadOnlyList<ProvisioningJobAttempt>> FindByJobIdAsync(Guid jobId, CancellationToken cancellationToken);
    }
}
