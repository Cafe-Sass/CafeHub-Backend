using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Branches.Contracts;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Branches.Queries
{
    public sealed class ListBranchesQueryHandler : IQueryHandler<ListBranchesQuery, OffsetPage<BranchResponse>>
    {
        private readonly IBranchStore branchStore;
        private readonly PaginationOptions paginationOptions;

        public ListBranchesQueryHandler(IBranchStore branchStore, PaginationOptions paginationOptions)
        {
            this.branchStore = branchStore ?? throw new ArgumentNullException(nameof(branchStore));
            this.paginationOptions = paginationOptions ?? throw new ArgumentNullException(nameof(paginationOptions));
        }

        public async Task<Result<OffsetPage<BranchResponse>>> Handle(ListBranchesQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var requestedSort = query.Sort is null
                ? null
                : new[] { new SortRequest(query.Sort, query.SortDirection) };
            var resolvedSort = BranchListSort.Whitelist.Resolve(requestedSort);
            if (resolvedSort.IsFailure)
            {
                return Result.Failure<OffsetPage<BranchResponse>>(resolvedSort.Error!);
            }

            var listPage = await branchStore.ListBranchesAsync(
                new BranchListRequest(query.Page, pageSize, resolvedSort.Value, query.IncludeTotalCount, query.Status),
                cancellationToken);

            var page = OffsetPage<BranchResponse>.Create(
                listPage.FetchedItems,
                query.Page,
                pageSize,
                listPage.TotalCount);

            return Result.Success(page);
        }
    }
}
