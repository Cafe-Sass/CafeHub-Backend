using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Menu.Queries
{
    public sealed class ListProductsQueryValidator : AbstractValidator<ListProductsQuery>
    {
        public ListProductsQueryValidator(PaginationOptions paginationOptions)
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

                    if (query.Kind is { } kind && !Enum.IsDefined(kind))
                    {
                        context.AddFailure(new ValidationFailure("kind", "The kind is not valid.")
                        {
                            ErrorCode = "Products.InvalidKind",
                        });
                    }

                    if (query.Status is { } status && !Enum.IsDefined(status))
                    {
                        context.AddFailure(new ValidationFailure("status", "The status is not valid.")
                        {
                            ErrorCode = "Products.InvalidStatus",
                        });
                    }

                    if (query.Tag is { } tag && !CafeHub.Domain.Tenants.ProductTag.IsValid(tag))
                    {
                        context.AddFailure(new ValidationFailure("tag", "The tag must be 1 to 32 lower-case letters, digits, hyphens, or spaces.")
                        {
                            ErrorCode = "Products.InvalidTag",
                        });
                    }
                });
        }

        private static void CopyFailures(ValidationResult result, ValidationContext<ListProductsQuery> context)
        {
            foreach (var failure in result.Errors)
            {
                context.AddFailure(failure);
            }
        }
    }
}
