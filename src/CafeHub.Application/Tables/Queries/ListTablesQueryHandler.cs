using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tables;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Tables.DTOs;

namespace CafeHub.Application.Tables.Queries
{
    public sealed class ListTablesQueryHandler : IQueryHandler<ListTablesQuery, OffsetPage<TableResponse>>
    {
        private readonly ITableStore tableStore;
        private readonly ICurrentTenantUser currentUser;
        private readonly PaginationOptions paginationOptions;

        public ListTablesQueryHandler(
            ITableStore tableStore,
            ICurrentTenantUser currentUser,
            PaginationOptions paginationOptions)
        {
            this.tableStore = tableStore ?? throw new ArgumentNullException(nameof(tableStore));
            this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            this.paginationOptions = paginationOptions ?? throw new ArgumentNullException(nameof(paginationOptions));
        }

        public async Task<Result<OffsetPage<TableResponse>>> Handle(
            ListTablesQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OffsetPage<TableResponse>>(scope.Error!);
            }

            if (scope.Value is { } branchId && branchId != query.BranchId)
            {
                return Result.Failure<OffsetPage<TableResponse>>(TablesErrors.NotFound());
            }

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var listPage = await tableStore.ListAsync(
                new TableListRequest(
                    query.BranchId,
                    query.Page,
                    pageSize,
                    query.IncludeTotalCount,
                    query.Status,
                    query.FromNumber,
                    query.ToNumber),
                cancellationToken);

            var page = OffsetPage<TableResponse>.Create(
                listPage.Items,
                query.Page,
                pageSize,
                listPage.TotalCount);

            return Result.Success(page);
        }
    }
}
