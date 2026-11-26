using CafeHub.Domain.Plans;
using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Subscriptions.Contracts
{
    public interface ISubscriptionRenewalStore
    {
        Task<Subscription?> FindSubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken);

        Task<Subscription?> FindSubscriptionByTenantAsync(
            Guid tenantId,
            CancellationToken cancellationToken);

        Task<SubscriptionRenewalRequest?> FindRequestAsync(
            Guid renewalRequestId,
            CancellationToken cancellationToken);

        Task<SubscriptionRenewalRequest?> FindOpenRequestAsync(
            Guid subscriptionId,
            CancellationToken cancellationToken);

        Task<SubscriptionPeriod?> FindLatestPeriodAsync(
            Guid subscriptionId,
            CancellationToken cancellationToken);

        Task<SubscriptionPeriod?> FindPeriodByRenewalRequestAsync(
            Guid renewalRequestId,
            CancellationToken cancellationToken);

        Task<PlanVersion?> FindPlanVersionAsync(Guid planVersionId, CancellationToken cancellationToken);

        void AddRequest(SubscriptionRenewalRequest request);

        void AddPeriod(SubscriptionPeriod period);

        void AddHistory(SubscriptionHistory history);
    }
}
