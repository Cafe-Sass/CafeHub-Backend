namespace CafeHub.Application.Provisioning.Contracts
{
    /// <summary>
    /// Use-case port for running a claimed provisioning job. Implemented by the provisioning orchestrator.
    /// </summary>
    public interface IProvisioningJobRunner
    {
        Task RunAsync(ClaimedProvisioningJob claimedJob, CancellationToken cancellationToken);
    }
}

