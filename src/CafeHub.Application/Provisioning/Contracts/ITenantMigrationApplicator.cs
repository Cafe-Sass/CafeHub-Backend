using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Provisioning.Contracts
{
    /// <summary>
    /// Applies the Tenant migration to a named tenant database only, never to Master.
    /// Implemented in Infrastructure against the administrative connection.
    /// </summary>
    public interface ITenantMigrationApplicator
    {
        Task ApplyAsync(string databaseName, CancellationToken cancellationToken);
    }
}
