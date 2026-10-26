using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Onboarding.Contracts
{
    /// <summary>
    /// Persistence port for tenants, implemented in Infrastructure against the Master database.
    /// </summary>
    public interface ITenantStore
    {
        Task<Tenant?> FindByOnboardingRequestIdAsync(Guid onboardingRequestId, CancellationToken cancellationToken);
        Task AddAsync(Tenant tenant, CancellationToken cancellationToken);
    }
}
