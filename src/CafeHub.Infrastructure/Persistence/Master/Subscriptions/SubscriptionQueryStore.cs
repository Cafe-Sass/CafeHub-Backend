using CafeHub.Application.Pagination;
using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Application.Subscriptions.DTOs;
using CafeHub.Domain.Subscriptions;
using CafeHub.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master.Subscriptions
{
    internal sealed class SubscriptionQueryStore(MasterDbContext context) : ISubscriptionQueryStore
    {
        public async Task<SubscriptionOverviewResponse?> FindOverviewAsync(
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            var overview = await (
                from candidateSubscription in context.Subscriptions.AsNoTracking()
                join tenant in context.Tenants.AsNoTracking()
                    on candidateSubscription.TenantId equals (Guid?)tenant.Id
                where tenant.Id == tenantId
                select new
                {
                    Subscription = candidateSubscription,
                    TenantStatus = tenant.LifecycleStatus,
                }).FirstOrDefaultAsync(cancellationToken);
            if (overview is null)
            {
                return null;
            }

            var subscription = overview.Subscription;
            var period = await context.SubscriptionPeriods.AsNoTracking()
                .Where(candidate => candidate.SubscriptionId == subscription.Id)
                .OrderByDescending(candidate => candidate.PeriodNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (period is null)
            {
                return new SubscriptionOverviewResponse(
                    subscription.Id,
                    subscription.TenantId,
                    overview.TenantStatus.ToString(),
                    subscription.Status.ToString(),
                    null, null, null, null, null, null, null, null, null, null);
            }

            var planVersion = await context.PlanVersions.AsNoTracking()
                .FirstOrDefaultAsync(version => version.Id == period.PlanVersionId, cancellationToken);
            var plan = planVersion is null
                ? null
                : await context.Plans.AsNoTracking()
                    .FirstOrDefaultAsync(candidate => candidate.Id == planVersion.PlanId, cancellationToken);

            return new SubscriptionOverviewResponse(
                subscription.Id,
                subscription.TenantId,
                overview.TenantStatus.ToString(),
                subscription.Status.ToString(),
                planVersion?.Id,
                plan?.Code,
                plan?.DisplayName,
                planVersion?.VersionNumber,
                planVersion?.Price,
                planVersion?.CurrencyCode,
                period.PeriodNumber,
                period.PeriodStatus.ToString(),
                period.StartsAt,
                period.EndsAt);
        }

        public async Task<OffsetPage<SubscriptionHistoryItem>?> ListHistoryAsync(
            Guid tenantId,
            int page,
            int pageSize,
            bool includeTotalCount,
            CancellationToken cancellationToken)
        {
            var subscriptionId = await context.Subscriptions.AsNoTracking()
                .Where(subscription => subscription.TenantId == tenantId)
                .Select(subscription => (Guid?)subscription.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (subscriptionId is null)
            {
                return null;
            }

            var records = context.SubscriptionHistory.AsNoTracking()
                .Where(history => history.SubscriptionId == subscriptionId.Value);
            int? totalCount = includeTotalCount
                ? await records.CountAsync(cancellationToken)
                : null;

            var rows = await records
                .OrderByDescending(history => history.OccurredAt)
                .ThenByDescending(history => history.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize + 1)
                .ToListAsync(cancellationToken);

            return OffsetPage<SubscriptionHistoryItem>.Create(
                rows.Select(history => new SubscriptionHistoryItem(
                    history.Id,
                    history.FromStatus?.ToString(),
                    history.ToStatus.ToString(),
                    history.PlanVersionId,
                    history.PeriodNumber,
                    history.ActorReference,
                    history.Reason,
                    history.OccurredAt))
                    .ToArray(),
                page,
                pageSize,
                totalCount);
        }
    }
}
