using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Inventory.Queries
{
    public sealed class ListStockMovementsQueryHandler(
        IStockMovementStore store, ICurrentTenantUser currentUser, PaginationOptions paginationOptions)
        : IQueryHandler<ListStockMovementsQuery, OffsetPage<StockMovementResponse>>
    {
        public async Task<Result<OffsetPage<StockMovementResponse>>> Handle(
            ListStockMovementsQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OffsetPage<StockMovementResponse>>(scope.Error!);
            }

            if (scope.Value is { } scopedBranchId
                && query.BranchId is { } requestedBranchId
                && requestedBranchId != scopedBranchId)
            {
                return Result.Failure<OffsetPage<StockMovementResponse>>(InventoryBatchErrors.BranchNotFound());
            }

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var listPage = await store.ListAsync(
                new StockMovementListRequest(
                    scope.Value ?? query.BranchId,
                    query.ItemId,
                    query.Type,
                    query.CreatedFrom,
                    query.CreatedTo,
                    query.Page,
                    pageSize,
                    query.IncludeTotalCount),
                cancellationToken);

            var page = OffsetPage<StockMovementResponse>.Create(
                listPage.Items,
                query.Page,
                pageSize,
                listPage.TotalCount);

            return Result.Success(page);
        }
    }
}
