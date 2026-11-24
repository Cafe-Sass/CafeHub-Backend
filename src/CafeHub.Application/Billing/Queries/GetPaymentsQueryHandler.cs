using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Billing.Queries
{
    public sealed class GetPaymentsQueryHandler(
        IPaymentQueryStore store,
        ICurrentTenantUser currentUser,
        PaginationOptions paginationOptions)
        : IQueryHandler<GetPaymentsQuery, OffsetPage<PaymentListItem>>
    {
        public async Task<Result<OffsetPage<PaymentListItem>>> Handle(
            GetPaymentsQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OffsetPage<PaymentListItem>>(scope.Error!);
            }

            var branchId = scope.Value ?? query.BranchId;
            if (branchId is null)
            {
                return Result.Failure<OffsetPage<PaymentListItem>>(BillingErrors.BranchRequired());
            }

            if (scope.Value.HasValue && query.BranchId.HasValue && query.BranchId != scope.Value)
            {
                return Result.Failure<OffsetPage<PaymentListItem>>(BillingErrors.NotFound());
            }

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var listPage = await store.ListAsync(
                new PaymentListRequest(
                    query.Page,
                    pageSize,
                    query.IncludeTotalCount,
                    branchId,
                    query.BillId,
                    query.Status),
                cancellationToken);

            return Result.Success(OffsetPage<PaymentListItem>.Create(
                listPage.Entries.Select(PaymentListItem.From).ToArray(),
                query.Page,
                pageSize,
                listPage.TotalCount));
        }
    }
}
