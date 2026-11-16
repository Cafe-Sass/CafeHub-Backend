using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Menu.Queries
{
    public sealed record GetMenuCategoryTreeQuery(bool IncludeInactive, OffsetPageRequest Paging)
        : IQuery<OffsetPage<MenuCategoryTreeNodeResponse>>;
}
