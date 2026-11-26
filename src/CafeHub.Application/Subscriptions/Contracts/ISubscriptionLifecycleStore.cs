using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Subscriptions.Contracts
{
    public interface ISubscriptionLifecycleStore
    {
        Task<Subscription?> FindAsync(Guid subscriptionId, CancellationToken cancellationToken);

        Task<SubscriptionPeriod?> FindLatestPeriodAsync(
            Guid subscriptionId,
            CancellationToken cancellationToken);

        void AddHistory(SubscriptionHistory history);
    }
}
