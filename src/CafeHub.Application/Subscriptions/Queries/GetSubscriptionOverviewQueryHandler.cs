using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Application.Subscriptions.DTOs;

namespace CafeHub.Application.Subscriptions.Queries
{
    public sealed class GetSubscriptionOverviewQueryHandler(ISubscriptionQueryStore store)
        : IQueryHandler<GetSubscriptionOverviewQuery, SubscriptionOverviewResponse>
    {
        public async Task<Result<SubscriptionOverviewResponse>> Handle(
            GetSubscriptionOverviewQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var overview = await store.FindOverviewAsync(query.TenantKey, cancellationToken);
            return overview is null
                ? Result.Failure<SubscriptionOverviewResponse>(SubscriptionErrors.NotFound())
                : Result.Success(overview);
        }
    }
}
