using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Upgrades.Contracts
{
    public interface ITenantSchemaUpgradeRunClaimer
    {
        Task<Result<ClaimedTenantSchemaUpgradeRun?>> TryClaimNextAsync(
            string workerId,
            DateTimeOffset now,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken);
    }
}
