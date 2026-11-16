using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;

namespace CafeHub.Application.Menu.Queries
{
    public sealed class GetMenuCategoryQueryHandler(IMenuCategoryStore store) : IQueryHandler<GetMenuCategoryQuery, MenuCategoryResponse>
    {
        public async Task<Result<MenuCategoryResponse>> Handle(GetMenuCategoryQuery query, CancellationToken cancellationToken)
        {
            var category = await store.GetVisibleAsync(query.Id, cancellationToken);
            return category is null
                ? Result.Failure<MenuCategoryResponse>(MenuCategoryErrors.NotFound())
                : Result.Success(category);
        }
    }
}
