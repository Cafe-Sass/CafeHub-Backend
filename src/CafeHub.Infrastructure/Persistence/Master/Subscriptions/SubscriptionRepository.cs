using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master.Subscriptions
{
    internal sealed class SubscriptionRepository(MasterDbContext context) : ISubscriptionRepository
    {
        public Task<Subscription?> FindByOnboardingRequestIdAsync(
            Guid onboardingRequestId,
            CancellationToken cancellationToken)
        {
            return context.Subscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(subscription => subscription.OnboardingRequestId == onboardingRequestId, cancellationToken);
        }
    }
}
