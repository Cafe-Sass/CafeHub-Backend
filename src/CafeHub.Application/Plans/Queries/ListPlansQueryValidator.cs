using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Plans.Queries
{
    public sealed class ListPlansQueryValidator : AbstractValidator<ListPlansQuery>
    {
        public ListPlansQueryValidator(PaginationOptions paginationOptions)
        {
            ArgumentNullException.ThrowIfNull(paginationOptions);

            var pageValidator = new OffsetPageRequestValidator(paginationOptions);
            var sortValidator = new SortRequestValidator(PlanListSort.Whitelist);

            RuleFor(query => query)
                .Custom((query, context) =>
                {
                    CopyFailures(
                        pageValidator.Validate(new OffsetPageRequest
                        {
                            Page = query.Page,
                            PageSize = query.PageSize,
                            IncludeTotalCount = query.IncludeTotalCount,
                        }),
                        context);

                    if (query.Sort is not null)
                    {
                        CopyFailures(sortValidator.Validate(new SortRequest(query.Sort, query.SortDirection)), context);
                        return;
                    }

                    if (!Enum.IsDefined(query.SortDirection))
                    {
                        context.AddFailure(new ValidationFailure("sort", PaginationErrors.SortDirectionIsInvalid().Message)
                        {
                            ErrorCode = PaginationErrors.SortDirectionInvalidCode,
                        });
                    }
                });
        }

        private static void CopyFailures(ValidationResult result, ValidationContext<ListPlansQuery> context)
        {
            foreach (var failure in result.Errors)
            {
                context.AddFailure(failure);
            }
        }
    }
}
