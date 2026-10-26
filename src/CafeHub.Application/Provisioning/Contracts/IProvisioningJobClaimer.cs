using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Provisioning.Contracts
{
    /// <summary>
    /// Persistence port for claiming a due provisioning job with a lease, implemented in Infrastructure against the Master database.
    /// </summary>
    public interface IProvisioningJobClaimer
    {
        Task<Result<ClaimedProvisioningJob?>> TryClaimNextAsync(string workerId, DateTimeOffset now, TimeSpan leaseDuration, CancellationToken cancellationToken);
    }
}

