using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Inventory.Queries
{
    public sealed class ListInventoryBatchesQueryValidator : AbstractValidator<ListInventoryBatchesQuery>
    {
        public ListInventoryBatchesQueryValidator(PaginationOptions paginationOptions)
        {
            ArgumentNullException.ThrowIfNull(paginationOptions);

            var pageValidator = new OffsetPageRequestValidator(paginationOptions);

            RuleFor(query => query.BranchId)
                .NotEmpty();

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

                    if (query.ItemId is { } itemId && itemId == Guid.Empty)
                    {
                        context.AddFailure(new ValidationFailure("itemId", "The item id must not be empty."));
                    }

                    if (query.ExpiryFrom is { } from && query.ExpiryTo is { } to && from > to)
                    {
                        context.AddFailure(
                            new ValidationFailure("expiryFrom", InventoryBatchErrors.InvalidExpiryRangeMessage)
                            {
                                ErrorCode = InventoryBatchErrors.InvalidExpiryRangeCode,
                            });
                    }
                });
        }
    }
}
