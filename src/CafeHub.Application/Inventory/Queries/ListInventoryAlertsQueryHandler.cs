using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Inventory.Queries
{
    public sealed class ListInventoryAlertsQueryHandler(
        IInventoryAlertStore store,
        IInventoryAlertEvaluator evaluator,
        ICurrentTenantUser currentUser,
        PaginationOptions paginationOptions)
        : IQueryHandler<ListInventoryAlertsQuery, OffsetPage<InventoryAlertResponse>>
    {
        public async Task<Result<OffsetPage<InventoryAlertResponse>>> Handle(
            ListInventoryAlertsQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OffsetPage<InventoryAlertResponse>>(scope.Error!);
            }

            if (scope.Value is { } scopedBranchId
                && query.BranchId is { } requestedBranchId
                && requestedBranchId != scopedBranchId)
            {
                return Result.Failure<OffsetPage<InventoryAlertResponse>>(
                    InventoryBatchErrors.BranchNotFound());
            }

            var evaluated = await evaluator.EvaluateExpiryAsync(
                scope.Value ?? query.BranchId, null, cancellationToken);
            if (evaluated.IsFailure)
            {
                return Result.Failure<OffsetPage<InventoryAlertResponse>>(evaluated.Error!);
            }

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var listPage = await store.ListAsync(
                new InventoryAlertListRequest(
                    scope.Value ?? query.BranchId,
                    query.Kind,
                    query.State,
                    query.Page,
                    pageSize,
                    query.IncludeTotalCount),
                cancellationToken);

            var page = OffsetPage<InventoryAlertResponse>.Create(
                listPage.Items,
                query.Page,
                pageSize,
                listPage.TotalCount);

            return Result.Success(page);
        }
    }
}
