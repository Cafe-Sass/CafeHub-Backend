using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Refunds.Contracts;
using CafeHub.Application.Refunds.DTOs;

namespace CafeHub.Application.Refunds.Queries
{
    public sealed class GetRefundRequestsQueryHandler(
        IRefundRequestStore store,
        ICurrentTenantUser currentUser,
        PaginationOptions paginationOptions)
        : IQueryHandler<GetRefundRequestsQuery, OffsetPage<RefundRequestListItem>>
    {
        public async Task<Result<OffsetPage<RefundRequestListItem>>> Handle(
            GetRefundRequestsQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OffsetPage<RefundRequestListItem>>(scope.Error!);
            }

            if (scope.Value.HasValue && query.BranchId.HasValue && query.BranchId != scope.Value)
            {
                return Result.Failure<OffsetPage<RefundRequestListItem>>(RefundRequestErrors.NotFound());
            }

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var listPage = await store.ListAsync(
                new RefundRequestListRequest(
                    query.Page,
                    pageSize,
                    query.IncludeTotalCount,
                    scope.Value ?? query.BranchId,
                    query.Status,
                    query.AssignedUserId,
                    query.BillId),
                cancellationToken);

            return Result.Success(OffsetPage<RefundRequestListItem>.Create(
                listPage.Entries,
                query.Page,
                pageSize,
                listPage.TotalCount));
        }
    }
}
