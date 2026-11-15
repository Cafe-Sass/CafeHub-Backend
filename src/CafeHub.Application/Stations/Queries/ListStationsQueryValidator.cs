using CafeHub.Application.Pagination;
using CafeHub.Application.Stations;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Stations.Queries
{
    public sealed class ListStationsQueryValidator : AbstractValidator<ListStationsQuery>
    {
        public ListStationsQueryValidator(PaginationOptions paginationOptions)
        {
            ArgumentNullException.ThrowIfNull(paginationOptions);

            var pageValidator = new OffsetPageRequestValidator(paginationOptions);

            RuleFor(query => query.BranchId)
                .NotEmpty();

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

                    if (query.Status is { } status && !Enum.IsDefined(status))
                    {
                        context.AddFailure(new ValidationFailure("status", StationsErrors.InvalidStatusMessage)
                        {
                            ErrorCode = StationsErrors.InvalidStatusCode,
                        });
                    }
                });
        }

        private static void CopyFailures(ValidationResult result, ValidationContext<ListStationsQuery> context)
        {
            foreach (var failure in result.Errors)
            {
                context.AddFailure(failure);
            }
        }
    }
}
