using CafeHub.Application.Pagination;
using CafeHub.Application.Tables;
using CafeHub.Domain.Tenants;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Tables.Queries
{
    public sealed class ListTablesQueryValidator : AbstractValidator<ListTablesQuery>
    {
        public ListTablesQueryValidator(PaginationOptions paginationOptions)
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
                        context.AddFailure(new ValidationFailure("status", TablesErrors.InvalidStatusMessage)
                        {
                            ErrorCode = TablesErrors.InvalidStatusCode,
                        });
                    }

                    if (query.FromNumber is { } fromNumber
                        && (fromNumber < BranchTable.MinimumNumber || fromNumber > BranchTable.MaximumNumber))
                    {
                        context.AddFailure(new ValidationFailure("fromNumber", TablesErrors.NumberRangeExceededMessage)
                        {
                            ErrorCode = TablesErrors.NumberRangeExceededCode,
                        });
                    }

                    if (query.ToNumber is { } toNumber
                        && (toNumber < BranchTable.MinimumNumber || toNumber > BranchTable.MaximumNumber))
                    {
                        context.AddFailure(new ValidationFailure("toNumber", TablesErrors.NumberRangeExceededMessage)
                        {
                            ErrorCode = TablesErrors.NumberRangeExceededCode,
                        });
                    }

                    if (query.FromNumber is { } from && query.ToNumber is { } to && from > to)
                    {
                        context.AddFailure(new ValidationFailure("fromNumber", "The from number must not be greater than the to number.")
                        {
                            ErrorCode = "Tables.NumberRangeOrderInvalid",
                        });
                    }
                });
        }

        private static void CopyFailures(ValidationResult result, ValidationContext<ListTablesQuery> context)
        {
            foreach (var failure in result.Errors)
            {
                context.AddFailure(failure);
            }
        }
    }
}
