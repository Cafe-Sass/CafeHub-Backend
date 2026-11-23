using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Guests.Queries
{
    public sealed class GetGuestMutesQueryValidator : AbstractValidator<GetGuestMutesQuery>
    {
        public GetGuestMutesQueryValidator(PaginationOptions paginationOptions)
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

        private static void CopyFailures(ValidationResult source, ValidationContext<GetGuestMutesQuery> destination)
        {
            foreach (var failure in source.Errors)
            {
                destination.AddFailure(failure);
            }
        }
    }
}
