using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Domain.Subscriptions;
using CafeHub.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master.Subscriptions
{
    internal sealed class SubscriptionLifecycleStore(MasterDbContext context)
        : ISubscriptionLifecycleStore
    {
        public Task<Subscription?> FindAsync(
            Guid subscriptionId,
            CancellationToken cancellationToken) =>
            context.Subscriptions.FirstOrDefaultAsync(
                subscription => subscription.Id == subscriptionId,
                cancellationToken);

        public Task<SubscriptionPeriod?> FindLatestPeriodAsync(
            Guid subscriptionId,
            CancellationToken cancellationToken) =>
            context.SubscriptionPeriods.AsNoTracking()
                .Where(period => period.SubscriptionId == subscriptionId)
                .OrderByDescending(period => period.PeriodNumber)
                .FirstOrDefaultAsync(cancellationToken);

        public void AddHistory(SubscriptionHistory history) =>
            context.SubscriptionHistory.Add(history);
    }
}
