namespace CafeHub.Application.Upgrades.Contracts
{
    public interface ITenantSchemaUpgradeRunner
    {
        Task RunAsync(ClaimedTenantSchemaUpgradeRun claimedRun, CancellationToken cancellationToken);
    }
}
