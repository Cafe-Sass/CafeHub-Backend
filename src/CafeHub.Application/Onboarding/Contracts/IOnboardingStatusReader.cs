using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Provisioning;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Onboarding.Contracts
{
    public sealed record OnboardingStatusSnapshot(
        OnboardingRequestStatus RequestStatus,
        TenantLifecycleStatus? TenantStatus,
        ProvisioningJobStatus? JobStatus);

    /// <summary>
    /// Read-only projection for the public onboarding status endpoint, implemented in Infrastructure
    /// against the Master database. Loads only the state columns; no personal data.
    /// </summary>
    public interface IOnboardingStatusReader
    {
        Task<OnboardingStatusSnapshot?> FindByRequestIdAsync(Guid requestId, CancellationToken cancellationToken);
    }
}
