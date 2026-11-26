using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Inventory.Queries
{
    public sealed class ListStockLevelsQueryHandler(
        IInventoryBatchStore store, ICurrentTenantUser currentUser, PaginationOptions paginationOptions)
        : IQueryHandler<ListStockLevelsQuery, OffsetPage<StockLevelResponse>>
    {
        public async Task<Result<OffsetPage<StockLevelResponse>>> Handle(
            ListStockLevelsQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OffsetPage<StockLevelResponse>>(scope.Error!);
            }

            if (scope.Value is { } scopedBranchId
                && query.BranchId is { } requestedBranchId
                && requestedBranchId != scopedBranchId)
            {
                return Result.Failure<OffsetPage<StockLevelResponse>>(InventoryBatchErrors.BranchNotFound());
            }

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var listPage = await store.ListStockLevelsAsync(
                new InventoryStockLevelRequest(
                    scope.Value ?? query.BranchId,
                    query.ItemId,
                    query.Page,
                    pageSize,
                    query.IncludeTotalCount),
                cancellationToken);

            var page = OffsetPage<StockLevelResponse>.Create(
                listPage.Items,
                query.Page,
                pageSize,
                listPage.TotalCount);

            return Result.Success(page);
        }
    }
}
