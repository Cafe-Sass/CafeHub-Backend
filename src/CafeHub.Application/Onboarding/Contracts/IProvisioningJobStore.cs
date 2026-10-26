using CafeHub.Domain.Provisioning;

namespace CafeHub.Application.Onboarding.Contracts
{
    /// <summary>
    /// Persistence port for provisioning jobs, implemented in Infrastructure against the Master database.
    /// </summary>
    public interface IProvisioningJobStore
    {
        Task AddAsync(ProvisioningJob job, CancellationToken cancellationToken);

        Task<ProvisioningJob?> FindByIdAsync(Guid jobId, CancellationToken cancellationToken);

        Task<ProvisioningJob?> FindByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken);

        Task UpdateAsync(ProvisioningJob job, CancellationToken cancellationToken);
    }
}
