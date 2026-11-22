using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Menu.Queries
{
    public sealed class ListBranchProductAvailabilityQueryValidator
        : AbstractValidator<ListBranchProductAvailabilityQuery>
    {
        public ListBranchProductAvailabilityQueryValidator(PaginationOptions paginationOptions)
        {
            ArgumentNullException.ThrowIfNull(paginationOptions);

            var pageValidator = new OffsetPageRequestValidator(paginationOptions);

            RuleFor(query => query).Custom((query, context) =>
            {
                if (query.BranchId == Guid.Empty)
                {
                    context.AddFailure(new ValidationFailure("branchId", "The branch is required."));
                }

                if (query.CategoryId == Guid.Empty)
                {
                    context.AddFailure(new ValidationFailure("categoryId", "The category is not valid."));
                }

                var page = new OffsetPageRequest
                {
                    Page = query.Page,
                    PageSize = query.PageSize,
                    IncludeTotalCount = query.IncludeTotalCount,
                };
                foreach (var failure in pageValidator.Validate(page).Errors)
                {
                    context.AddFailure(failure);
                }
            });
        }
    }
}
