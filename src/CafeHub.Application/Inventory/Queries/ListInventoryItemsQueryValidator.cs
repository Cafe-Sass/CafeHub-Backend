using CafeHub.Application.Inventory;
using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Inventory.Queries
{
    public sealed class ListInventoryItemsQueryValidator : AbstractValidator<ListInventoryItemsQuery>
    {
        public ListInventoryItemsQueryValidator(PaginationOptions paginationOptions)
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

                    if (query.Status is { } status && !Enum.IsDefined(status))
                    {
                        context.AddFailure(new ValidationFailure("status", InventoryItemErrors.InvalidStatusMessage)
                        {
                            ErrorCode = InventoryItemErrors.InvalidStatusCode,
                        });
                    }
                });
        }
    }
}
