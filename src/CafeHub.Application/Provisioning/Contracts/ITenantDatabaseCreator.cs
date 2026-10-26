using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Provisioning.Contracts
{
    /// <summary>
    /// Creates the per-tenant PostgreSQL database and its dedicated role pair idempotently.
    /// Implemented in Infrastructure against the cluster through the administrative connection.
    /// </summary>
    public interface ITenantDatabaseCreator
    {
        Task<Result<TenantDatabaseInfo>> EnsureDatabaseAsync(TenantDatabaseSpec spec, CancellationToken cancellationToken);
    }
}
