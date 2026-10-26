using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Onboarding.Contracts
{
    /// <summary>
    /// Persistence port for subscriptions, implemented in Infrastructure against the Master database.
    /// </summary>
    public interface ISubscriptionRepository
    {
        Task<Subscription?> FindByOnboardingRequestIdAsync(Guid onboardingRequestId, CancellationToken cancellationToken);
    }
}
