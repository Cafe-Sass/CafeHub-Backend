using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Pagination;
using CafeHub.Application.Subscriptions.DTOs;

namespace CafeHub.Application.Subscriptions.Queries
{
    public sealed record GetSubscriptionHistoryQuery(
        Guid TenantKey,
        int Page = 1,
        int? PageSize = null,
        bool IncludeTotalCount = false) : IQuery<OffsetPage<SubscriptionHistoryItem>>;
}
