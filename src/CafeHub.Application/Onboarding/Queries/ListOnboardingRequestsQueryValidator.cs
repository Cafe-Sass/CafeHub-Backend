using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Onboarding;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Onboarding.Queries
{
    public sealed class ListOnboardingRequestsQueryValidator : AbstractValidator<ListOnboardingRequestsQuery>
    {
        public ListOnboardingRequestsQueryValidator(PaginationOptions paginationOptions)
        {
            ArgumentNullException.ThrowIfNull(paginationOptions);

            var pageValidator = new OffsetPageRequestValidator(paginationOptions);
            var sortValidator = new SortRequestValidator(OnboardingRequestListSort.Whitelist);

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

                    if (query.State is not null && !Enum.TryParse<OnboardingRequestStatus>(query.State, ignoreCase: true, out _))
                    {
                        context.AddFailure(new ValidationFailure("state", OnboardingErrors.RequestStateInvalid(query.State).Message)
                        {
                            ErrorCode = OnboardingErrors.RequestStateInvalidCode,
                        });
                    }
                });
        }

        private static void CopyFailures(ValidationResult result, ValidationContext<ListOnboardingRequestsQuery> context)
        {
            foreach (var failure in result.Errors)
            {
                context.AddFailure(failure);
            }
        }
    }
}
