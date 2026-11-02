using CafeHub.Application.Branches;
using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Branches.Queries
{
    public sealed class ListBranchesQueryValidator : AbstractValidator<ListBranchesQuery>
    {
        public ListBranchesQueryValidator(PaginationOptions paginationOptions)
        {
            ArgumentNullException.ThrowIfNull(paginationOptions);

            var pageValidator = new OffsetPageRequestValidator(paginationOptions);
            var sortValidator = new SortRequestValidator(BranchListSort.Whitelist);

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

                    if (query.Status is { } status && !Enum.IsDefined(status))
                    {
                        context.AddFailure(new ValidationFailure("status", BranchesErrors.InvalidStatusMessage)
                        {
                            ErrorCode = BranchesErrors.InvalidStatusCode,
                        });
                    }
                });
        }

        private static void CopyFailures(ValidationResult result, ValidationContext<ListBranchesQuery> context)
        {
            foreach (var failure in result.Errors)
            {
                context.AddFailure(failure);
            }
        }
    }
}
