using CafeHub.Application.Pagination;
using CafeHub.Application.Subscriptions.DTOs;

namespace CafeHub.Application.Subscriptions.Contracts
{
    public interface ISubscriptionQueryStore
    {
        Task<SubscriptionOverviewResponse?> FindOverviewAsync(
            Guid tenantId,
            CancellationToken cancellationToken);

        Task<OffsetPage<SubscriptionHistoryItem>?> ListHistoryAsync(
            Guid tenantId,
            int page,
            int pageSize,
            bool includeTotalCount,
            CancellationToken cancellationToken);
    }
}
