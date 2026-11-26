using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Domain.Plans;
using CafeHub.Domain.Subscriptions;
using CafeHub.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master.Subscriptions
{
    internal sealed class SubscriptionRenewalStore(MasterDbContext context) : ISubscriptionRenewalStore
    {
        public Task<Subscription?> FindSubscriptionAsync(
            Guid subscriptionId,
            CancellationToken cancellationToken) =>
            context.Subscriptions.FirstOrDefaultAsync(
                subscription => subscription.Id == subscriptionId,
                cancellationToken);

        public Task<Subscription?> FindSubscriptionByTenantAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            context.Subscriptions.FirstOrDefaultAsync(
                subscription => subscription.TenantId == tenantId,
                cancellationToken);

        public Task<SubscriptionRenewalRequest?> FindRequestAsync(
            Guid renewalRequestId,
            CancellationToken cancellationToken) =>
            context.SubscriptionRenewalRequests.FirstOrDefaultAsync(
                request => request.Id == renewalRequestId,
                cancellationToken);

        public Task<SubscriptionRenewalRequest?> FindOpenRequestAsync(
            Guid subscriptionId,
            CancellationToken cancellationToken) =>
            context.SubscriptionRenewalRequests
                .Where(request => request.SubscriptionId == subscriptionId
                    && (request.Status == RenewalRequestStatus.Pending
                        || request.Status == RenewalRequestStatus.Approved))
                .OrderByDescending(request => request.RequestedAt)
                .FirstOrDefaultAsync(cancellationToken);

        public Task<SubscriptionPeriod?> FindLatestPeriodAsync(
            Guid subscriptionId,
            CancellationToken cancellationToken) =>
            context.SubscriptionPeriods
                .Where(period => period.SubscriptionId == subscriptionId)
                .OrderByDescending(period => period.PeriodNumber)
                .FirstOrDefaultAsync(cancellationToken);

        public Task<SubscriptionPeriod?> FindPeriodByRenewalRequestAsync(
            Guid renewalRequestId,
            CancellationToken cancellationToken) =>
            context.SubscriptionPeriods.AsNoTracking()
                .FirstOrDefaultAsync(
                    period => period.RenewalRequestId == renewalRequestId,
                    cancellationToken);

        public Task<PlanVersion?> FindPlanVersionAsync(
            Guid planVersionId,
            CancellationToken cancellationToken) =>
            context.PlanVersions.AsNoTracking()
                .FirstOrDefaultAsync(version => version.Id == planVersionId, cancellationToken);

        public void AddRequest(SubscriptionRenewalRequest request) =>
            context.SubscriptionRenewalRequests.Add(request);

        public void AddPeriod(SubscriptionPeriod period) => context.SubscriptionPeriods.Add(period);

        public void AddHistory(SubscriptionHistory history) => context.SubscriptionHistory.Add(history);
    }
}
