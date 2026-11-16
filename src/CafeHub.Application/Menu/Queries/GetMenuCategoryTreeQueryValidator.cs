using CafeHub.Application.Pagination;
using FluentValidation;

namespace CafeHub.Application.Menu.Queries
{
    public sealed class GetMenuCategoryTreeQueryValidator : AbstractValidator<GetMenuCategoryTreeQuery>
    {
        public GetMenuCategoryTreeQueryValidator(PaginationOptions options)
        {
            var pagingValidator = new OffsetPageRequestValidator(options);
            RuleFor(query => query.Paging).Custom((paging, context) =>
            {
                foreach (var failure in pagingValidator.Validate(paging).Errors)
                {
                    context.AddFailure(failure);
                }

                if (((long)paging.Page - 1) * (paging.PageSize ?? options.DefaultPageSize) > int.MaxValue)
                {
                    context.AddFailure("page", "The page exceeds the supported offset.");
                }
            });
        }
    }
}
