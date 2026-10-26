using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Provisioning.Contracts
{
    /// <summary>
    /// Applies the Tenant migration to a named tenant database and seeds the Owner account,
    /// cafe profile, and reference data idempotently. Implemented in Infrastructure.
    /// </summary>
    public interface ITenantSchemaInitializer
    {
        Task<Result> InitializeAsync(TenantInitializationRequest request, CancellationToken cancellationToken);
    }
}
