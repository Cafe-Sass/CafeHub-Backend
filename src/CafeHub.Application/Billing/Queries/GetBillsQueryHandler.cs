using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Billing.Queries
{
    public sealed class GetBillsQueryHandler(
        IBillQueryStore store,
        ICurrentTenantUser currentUser,
        PaginationOptions paginationOptions)
        : IQueryHandler<GetBillsQuery, OffsetPage<BillListItem>>
    {
        public async Task<Result<OffsetPage<BillListItem>>> Handle(
            GetBillsQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OffsetPage<BillListItem>>(scope.Error!);
            }

            var branchId = scope.Value ?? query.BranchId;
            if (branchId is null)
            {
                return Result.Failure<OffsetPage<BillListItem>>(BillingErrors.BranchRequired());
            }

            if (scope.Value.HasValue && query.BranchId.HasValue && query.BranchId != scope.Value)
            {
                return Result.Failure<OffsetPage<BillListItem>>(BillingErrors.NotFound());
            }

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var listPage = await store.ListAsync(
                new BillListRequest(
                    query.Page,
                    pageSize,
                    query.IncludeTotalCount,
                    branchId,
                    query.TableId,
                    query.Status),
                cancellationToken);

            return Result.Success(OffsetPage<BillListItem>.Create(
                listPage.Entries.Select(BillListItem.From).ToArray(),
                query.Page,
                pageSize,
                listPage.TotalCount));
        }
    }
}
