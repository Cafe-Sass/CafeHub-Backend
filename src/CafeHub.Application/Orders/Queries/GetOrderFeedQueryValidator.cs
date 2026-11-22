using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Orders.Queries
{
    public sealed class GetOrderFeedQueryValidator : AbstractValidator<GetOrderFeedQuery>
    {
        public GetOrderFeedQueryValidator(PaginationOptions paginationOptions)
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

        private static void CopyFailures(ValidationResult source, ValidationContext<GetOrderFeedQuery> destination)
        {
            foreach (var failure in source.Errors)
            {
                destination.AddFailure(failure);
            }
        }
    }
}
