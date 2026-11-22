using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Orders.Commands;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Orders.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Application.Stations.Contracts;
using CafeHub.Domain.Tenants;
using MediatR;

namespace CafeHub.Application.Orders.Queries
{
    public sealed class GetOrderFeedQueryHandler(
        ISender sender,
        IOrderFeedStore feedStore,
        ICurrentTenantUser currentUser,
        IStationScope stationScope,
        PaginationOptions paginationOptions)
        : IQueryHandler<GetOrderFeedQuery, OffsetPage<OrderFeedItem>>
    {
        public async Task<Result<OffsetPage<OrderFeedItem>>> Handle(
            GetOrderFeedQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OffsetPage<OrderFeedItem>>(scope.Error!);
            }

            var branchId = scope.Value ?? query.BranchId;
            if (branchId is null)
            {
                return Result.Failure<OffsetPage<OrderFeedItem>>(OrdersErrors.BranchRequired());
            }

            if (scope.Value.HasValue && query.BranchId.HasValue && query.BranchId != scope.Value)
            {
                return Result.Failure<OffsetPage<OrderFeedItem>>(OrdersErrors.NotFound());
            }

            await sender.Send(new ExpireStaleOrdersCommand(branchId, null), cancellationToken);

            OrderStatus? status = null;
            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                if (!Enum.TryParse<OrderStatus>(query.Status, ignoreCase: true, out var parsed)
                    || !Enum.IsDefined(parsed)
                    || !parsed.ToString().Equals(query.Status, StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Failure<OffsetPage<OrderFeedItem>>(OrdersErrors.StatusInvalid());
                }

                status = parsed;
            }

            IReadOnlyList<Guid>? visibleStationIds = null;
            if (scope.Value is not null && currentUser.UserId is { } userId)
            {
                visibleStationIds = await stationScope.GetVisibleStationIdsAsync(userId, cancellationToken);
            }

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var listPage = await feedStore.ListFeedAsync(
                new OrderFeedListRequest(
                    query.Page,
                    pageSize,
                    query.IncludeTotalCount,
                    branchId.Value,
                    status,
                    query.UpdatedSince,
                    visibleStationIds),
                cancellationToken);

            var visible = OrderFeedStationFilter.Apply(listPage.FetchedItems, visibleStationIds);
            var page = OffsetPage<OrderFeedItem>.Create(
                visible.Select(OrderFeedItem.From).ToArray(),
                query.Page,
                pageSize,
                listPage.TotalCount);

            return Result.Success(page);
        }
    }
}
