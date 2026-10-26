using CafeHub.Domain.Provisioning;

namespace CafeHub.Application.Onboarding.Contracts
{
    /// <summary>
    /// Persistence port for provisioning jobs, implemented in Infrastructure against the Master database.
    /// </summary>
    public interface IProvisioningJobStore
    {
        Task AddAsync(ProvisioningJob job, CancellationToken cancellationToken);
    }
}
