using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Orders.DTOs;
using CafeHub.Application.Stations.Contracts;

namespace CafeHub.Application.Orders.Queries
{
    public sealed class GetOrderQueryHandler(
        IOrderFeedStore feedStore,
        ICurrentTenantUser currentUser,
        IStationScope stationScope)
        : IQueryHandler<GetOrderQuery, OrderFeedItem>
    {
        public async Task<Result<OrderFeedItem>> Handle(
            GetOrderQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OrderFeedItem>(scope.Error!);
            }

            IReadOnlyList<Guid>? visibleStationIds = null;
            if (scope.Value is not null && currentUser.UserId is { } userId)
            {
                visibleStationIds = await stationScope.GetVisibleStationIdsAsync(userId, cancellationToken);
            }

            var entry = await feedStore.FindByIdAsync(scope.Value, query.OrderId, cancellationToken);
            if (entry is null)
            {
                return Result.Failure<OrderFeedItem>(OrdersErrors.NotFound());
            }

            var visible = OrderFeedStationFilter.Apply([entry], visibleStationIds);
            if (visible.Count == 0)
            {
                return Result.Failure<OrderFeedItem>(OrdersErrors.NotFound());
            }

            return Result.Success(OrderFeedItem.From(visible[0]));
        }
    }
}
