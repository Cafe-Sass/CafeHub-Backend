using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Inventory.Queries
{
    public sealed class ListInventoryBatchesQueryHandler(
        IInventoryBatchStore store, ICurrentTenantUser currentUser, PaginationOptions paginationOptions)
        : IQueryHandler<ListInventoryBatchesQuery, OffsetPage<InventoryBatchResponse>>
    {
        public async Task<Result<OffsetPage<InventoryBatchResponse>>> Handle(
            ListInventoryBatchesQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OffsetPage<InventoryBatchResponse>>(scope.Error!);
            }

            if (scope.Value is { } scopedBranchId && scopedBranchId != query.BranchId)
            {
                return Result.Failure<OffsetPage<InventoryBatchResponse>>(InventoryBatchErrors.BranchNotFound());
            }

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var listPage = await store.ListAsync(
                new InventoryBatchListRequest(
                    query.BranchId,
                    query.ItemId,
                    query.ExpiryFrom,
                    query.ExpiryTo,
                    query.Page,
                    pageSize,
                    query.IncludeTotalCount),
                cancellationToken);
            if (listPage.IsFailure)
            {
                return Result.Failure<OffsetPage<InventoryBatchResponse>>(listPage.Error!);
            }

            var page = OffsetPage<InventoryBatchResponse>.Create(
                listPage.Value.Items,
                query.Page,
                pageSize,
                listPage.Value.TotalCount);

            return Result.Success(page);
        }
    }
}
