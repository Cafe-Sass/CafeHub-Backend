using FluentValidation;

namespace CafeHub.Application.Menu.Queries
{
    public sealed class GetMenuCategoryQueryValidator : AbstractValidator<GetMenuCategoryQuery>
    {
        public GetMenuCategoryQueryValidator()
        {
            RuleFor(query => query.Id).NotEmpty();
        }
    }
}
