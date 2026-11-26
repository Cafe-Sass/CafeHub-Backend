using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Subscriptions.DTOs;

namespace CafeHub.Application.Subscriptions.Queries
{
    public sealed record GetSubscriptionOverviewQuery(Guid TenantKey)
        : IQuery<SubscriptionOverviewResponse>;
}
