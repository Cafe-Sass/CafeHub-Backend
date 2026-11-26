using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Pagination;
using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Application.Subscriptions.DTOs;

namespace CafeHub.Application.Subscriptions.Queries
{
    public sealed class GetSubscriptionHistoryQueryHandler(
        ISubscriptionQueryStore store,
        PaginationOptions paginationOptions)
        : IQueryHandler<GetSubscriptionHistoryQuery, OffsetPage<SubscriptionHistoryItem>>
    {
        public async Task<Result<OffsetPage<SubscriptionHistoryItem>>> Handle(
            GetSubscriptionHistoryQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var page = await store.ListHistoryAsync(
                query.TenantKey,
                query.Page,
                pageSize,
                query.IncludeTotalCount,
                cancellationToken);
            return page is null
                ? Result.Failure<OffsetPage<SubscriptionHistoryItem>>(SubscriptionErrors.NotFound())
                : Result.Success(page);
        }
    }
}
