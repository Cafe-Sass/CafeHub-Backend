using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Menu.Queries
{
    public sealed class ListProductsQueryHandler(IProductStore store, PaginationOptions paginationOptions)
        : IQueryHandler<ListProductsQuery, OffsetPage<ProductListItemResponse>>
    {
        public async Task<Result<OffsetPage<ProductListItemResponse>>> Handle(
            ListProductsQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var requestedSort = query.Sort is null
                ? null
                : new[] { new SortRequest(query.Sort, query.SortDirection) };
            var resolvedSort = ProductListSort.Whitelist.Resolve(requestedSort);
            if (resolvedSort.IsFailure)
            {
                return Result.Failure<OffsetPage<ProductListItemResponse>>(resolvedSort.Error!);
            }

            var page = await store.ListAsync(
                query.CategoryId,
                query.Kind,
                query.Caffeine,
                query.Tag,
                query.Status,
                query.Search,
                query.Page,
                pageSize,
                resolvedSort.Value,
                query.IncludeTotalCount,
                cancellationToken);
            return Result.Success(page);
        }
    }
}
