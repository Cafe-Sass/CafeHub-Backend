using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Inventory.Queries
{
    public sealed class ListInventoryAlertsQueryValidator : AbstractValidator<ListInventoryAlertsQuery>
    {
        public ListInventoryAlertsQueryValidator(PaginationOptions paginationOptions)
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
                        context.AddFailure(new ValidationFailure(
                            "branchId", "The branch id must not be empty."));
                    }

                    if (query.Kind is { } kind && !Enum.IsDefined(kind))
                    {
                        context.AddFailure(new ValidationFailure(
                            "kind", InventoryAlertErrors.InvalidKindFilter().Message)
                        {
                            ErrorCode = InventoryAlertErrors.InvalidKindFilterCode,
                        });
                    }

                    if (query.State is { } state && !Enum.IsDefined(state))
                    {
                        context.AddFailure(new ValidationFailure(
                            "state", InventoryAlertErrors.InvalidStateFilter().Message)
                        {
                            ErrorCode = InventoryAlertErrors.InvalidStateFilterCode,
                        });
                    }
                });
        }
    }
}
