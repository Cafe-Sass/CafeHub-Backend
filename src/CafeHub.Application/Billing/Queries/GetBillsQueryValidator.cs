using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Billing.Queries
{
    public sealed class GetBillsQueryValidator : AbstractValidator<GetBillsQuery>
    {
        public GetBillsQueryValidator(PaginationOptions paginationOptions)
        {
            ArgumentNullException.ThrowIfNull(paginationOptions);

            var pageValidator = new OffsetPageRequestValidator(paginationOptions);

            RuleFor(query => query.BranchId)
                .Must(branchId => branchId is null || branchId != Guid.Empty)
                .WithMessage("The branch id cannot be empty.");

            RuleFor(query => query.TableId)
                .Must(tableId => tableId is null || tableId != Guid.Empty)
                .WithMessage("The table id cannot be empty.");

            RuleFor(query => query)
                .Custom((query, context) =>
                {
                    var pageResult = pageValidator.Validate(new OffsetPageRequest
                    {
                        Page = query.Page,
                        PageSize = query.PageSize,
                        IncludeTotalCount = query.IncludeTotalCount,
                    });

                    foreach (var failure in pageResult.Errors)
                    {
                        context.AddFailure(failure);
                    }
                });
        }
    }
}
