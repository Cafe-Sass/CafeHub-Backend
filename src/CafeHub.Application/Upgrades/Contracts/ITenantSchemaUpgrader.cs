using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Upgrades.Contracts
{
    public interface ITenantSchemaUpgrader
    {
        Task<Result<string?>> UpgradeAsync(string databaseName, CancellationToken cancellationToken);
    }
}
