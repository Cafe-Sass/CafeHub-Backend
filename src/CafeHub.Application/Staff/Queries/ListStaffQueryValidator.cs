using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Staff.Queries
{
    public sealed class ListStaffQueryValidator : AbstractValidator<ListStaffQuery>
    {
        public ListStaffQueryValidator(PaginationOptions paginationOptions)
        {
            ArgumentNullException.ThrowIfNull(paginationOptions);

            var pageValidator = new OffsetPageRequestValidator(paginationOptions);

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
                });
        }

        private static void CopyFailures(ValidationResult source, ValidationContext<ListStaffQuery> destination)
        {
            foreach (var failure in source.Errors)
            {
                destination.AddFailure(failure);
            }
        }
    }
}
