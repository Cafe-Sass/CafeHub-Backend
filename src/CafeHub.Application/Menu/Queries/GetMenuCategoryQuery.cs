using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Menu.DTOs;

namespace CafeHub.Application.Menu.Queries
{
    public sealed record GetMenuCategoryQuery(Guid Id) : IQuery<MenuCategoryResponse>;
}
