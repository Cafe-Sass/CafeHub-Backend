using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Inventory.Queries
{
    public sealed class ListStockMovementsQueryValidator : AbstractValidator<ListStockMovementsQuery>
    {
        public ListStockMovementsQueryValidator(PaginationOptions paginationOptions)
        {
            ArgumentNullException.ThrowIfNull(paginationOptions);

            var pageValidator = new OffsetPageRequestValidator(paginationOptions);

            RuleFor(query => query)
                .Custom((query, context) =>
                {
                    foreach (var failure in pageValidator.Validate(new OffsetPageRequest
                    {
                        Page = query.Page,
                        PageSize = query.PageSize,
                        IncludeTotalCount = query.IncludeTotalCount,
                    }).Errors)
                    {
                        context.AddFailure(failure);
                    }

                    if (query.BranchId is { } branchId && branchId == Guid.Empty)
                    {
                        context.AddFailure(new ValidationFailure("branchId", "The branch id must not be empty."));
                    }

                    if (query.ItemId is { } itemId && itemId == Guid.Empty)
                    {
                        context.AddFailure(new ValidationFailure("itemId", "The item id must not be empty."));
                    }

                    if (query.Type is { } type && !Enum.IsDefined(type))
                    {
                        context.AddFailure(new ValidationFailure("type", StockMovementErrors.InvalidTypeFilter().Message)
                        {
                            ErrorCode = StockMovementErrors.InvalidTypeFilterCode,
                        });
                    }

                    if (query.CreatedFrom is { } from
                        && query.CreatedTo is { } to
                        && from > to)
                    {
                        context.AddFailure(new ValidationFailure("createdFrom", StockMovementErrors.InvalidDateRange().Message)
                        {
                            ErrorCode = StockMovementErrors.InvalidDateRangeCode,
                        });
                    }
                });
        }
    }
}
