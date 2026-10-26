using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Onboarding.Contracts
{
    public interface IPaymentTransactionStore
    {
        Task<PlatformPaymentTransaction?> FindByIdempotencyKeyAsync(Guid onboardingRequestId, string idempotencyKey, CancellationToken cancellationToken);
        Task<PlatformPaymentTransaction?> FindByOnboardingRequestIdAsync(Guid onboardingRequestId, CancellationToken cancellationToken);
        Task<bool> KeyExistsAsync(string idempotencyKey, CancellationToken cancellationToken);
        Task AddAsync(PlatformPaymentTransaction transaction, CancellationToken cancellationToken);
    }
}
