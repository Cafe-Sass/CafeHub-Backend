using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Provisioning.Contracts
{
    /// <summary>
    /// Seeds the Owner account, the Owner role and membership, the cafe profile shell, and the
    /// reference data into a tenant database in one idempotent transaction.
    /// Implemented in Infrastructure.
    /// </summary>
    public interface ITenantSeeder
    {
        Task<Result> SeedAsync(TenantSeedData seedData, CancellationToken cancellationToken);
    }
}
