using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Menu.Queries
{
    public sealed class GetGuestMenuQueryValidator : AbstractValidator<GetGuestMenuQuery>
    {
        public GetGuestMenuQueryValidator(PaginationOptions paginationOptions)
        {
            ArgumentNullException.ThrowIfNull(paginationOptions);

            var pageValidator = new OffsetPageRequestValidator(paginationOptions);
            var sortValidator = new SortRequestValidator(ProductListSort.Whitelist);

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

                    if (query.Sort is not null
                        && !CopyFailures(sortValidator.Validate(new SortRequest(query.Sort, query.SortDirection)), context))
                    {
                        return;
                    }

                    if (query.Sort is null && !Enum.IsDefined(query.SortDirection))
                    {
                        context.AddFailure(new ValidationFailure("sort", PaginationErrors.SortDirectionIsInvalid().Message)
                        {
                            ErrorCode = PaginationErrors.SortDirectionInvalidCode,
                        });
                    }

                    if (query.Kind is { } kind && !Enum.IsDefined(kind))
                    {
                        context.AddFailure(new ValidationFailure("kind", "The kind is not valid.")
                        {
                            ErrorCode = "GuestMenu.InvalidKind",
                        });
                    }

                    if (query.Tag is { } tag && !ProductTag.IsValid(tag))
                    {
                        context.AddFailure(new ValidationFailure("tag", "The tag must be 1 to 32 lower-case letters, digits, hyphens, or spaces.")
                        {
                            ErrorCode = "GuestMenu.InvalidTag",
                        });
                    }
                });
        }

        private static bool CopyFailures(ValidationResult result, ValidationContext<GetGuestMenuQuery> context)
        {
            foreach (var failure in result.Errors)
            {
                context.AddFailure(failure);
            }

            return result.IsValid;
        }
    }
}
