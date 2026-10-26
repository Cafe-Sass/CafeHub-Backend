namespace CafeHub.Application.Provisioning
{
    /// <summary>
    /// A provisioning job claimed by a worker, carrying the identifiers the runner needs to execute it.
    /// </summary>
    public sealed record ClaimedProvisioningJob(Guid JobId, Guid TenantId);
}
