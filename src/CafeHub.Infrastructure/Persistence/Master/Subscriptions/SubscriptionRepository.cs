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

        public Task AddAsync(
            Subscription subscription,
            SubscriptionPeriod initialPeriod,
            CancellationToken cancellationToken)
        {
            context.Subscriptions.Add(subscription);
            context.SubscriptionPeriods.Add(initialPeriod);
            context.SubscriptionHistory.Add(SubscriptionHistory.Create(
                Guid.CreateVersion7(),
                subscription.Id,
                null,
                subscription.Status,
                initialPeriod.PlanVersionId,
                initialPeriod.PeriodNumber,
                null,
                null,
                null,
                initialPeriod.CreatedAt));
            return Task.CompletedTask;
        }
    }
}
