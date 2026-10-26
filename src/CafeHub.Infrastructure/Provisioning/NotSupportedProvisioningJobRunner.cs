using CafeHub.Application.Provisioning;
using CafeHub.Application.Provisioning.Contracts;

namespace CafeHub.Infrastructure.Provisioning
{
    /// <summary>
    /// Placeholder runner that throws until the real provisioning orchestrator is implemented in a later sub-task.
    /// The worker is disabled by default so this runner is never invoked before the orchestrator exists.
    /// </summary>
    internal sealed class NotSupportedProvisioningJobRunner : IProvisioningJobRunner
    {
        public Task RunAsync(ClaimedProvisioningJob claimedJob, CancellationToken cancellationToken)
        {
            throw new NotSupportedException(
                "The provisioning job runner is not implemented yet. Register the provisioning orchestrator as IProvisioningJobRunner before enabling the worker.");
        }
    }
}

