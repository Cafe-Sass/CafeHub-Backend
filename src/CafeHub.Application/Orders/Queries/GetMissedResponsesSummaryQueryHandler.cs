using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Orders.Queries
{
    public sealed class GetMissedResponsesSummaryQueryHandler(
        IMissedResponseStore summaryStore,
        ICurrentTenantUser currentUser,
        PaginationOptions paginationOptions)
        : IQueryHandler<GetMissedResponsesSummaryQuery, OffsetPage<MissedResponseSummaryEntry>>
    {
        public async Task<Result<OffsetPage<MissedResponseSummaryEntry>>> Handle(
            GetMissedResponsesSummaryQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OffsetPage<MissedResponseSummaryEntry>>(scope.Error!);
            }

            var branchId = scope.Value ?? query.BranchId;
            if (branchId is null)
            {
                return Result.Failure<OffsetPage<MissedResponseSummaryEntry>>(OrdersErrors.BranchRequired());
            }

            if (scope.Value.HasValue && query.BranchId.HasValue && query.BranchId != scope.Value)
            {
                return Result.Failure<OffsetPage<MissedResponseSummaryEntry>>(OrdersErrors.NotFound());
            }

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var listPage = await summaryStore.ListSummaryAsync(
                new MissedResponseSummaryRequest(
                    query.Page,
                    pageSize,
                    query.IncludeTotalCount,
                    branchId.Value,
                    query.From,
                    query.To),
                cancellationToken);

            var page = OffsetPage<MissedResponseSummaryEntry>.Create(
                listPage.Entries.ToArray(),
                query.Page,
                pageSize,
                listPage.TotalCount);

            return Result.Success(page);
        }
    }
}
