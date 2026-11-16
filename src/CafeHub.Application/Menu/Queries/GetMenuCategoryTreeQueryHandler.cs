using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Menu.Queries
{
    public sealed class GetMenuCategoryTreeQueryHandler(IMenuCategoryStore store, PaginationOptions options)
        : IQueryHandler<GetMenuCategoryTreeQuery, OffsetPage<MenuCategoryTreeNodeResponse>>
    {
        public async Task<Result<OffsetPage<MenuCategoryTreeNodeResponse>>> Handle(
            GetMenuCategoryTreeQuery query, CancellationToken cancellationToken)
        {
            var paging = query.Paging;
            return Result.Success(await store.GetTreeAsync(
                query.IncludeInactive, paging.Page, options.ResolvePageSize(paging.PageSize),
                paging.IncludeTotalCount, cancellationToken));
        }
    }
}
