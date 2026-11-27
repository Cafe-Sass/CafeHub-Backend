using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Refunds.Queries
{
    public sealed class GetRefundRequestsQueryValidator : AbstractValidator<GetRefundRequestsQuery>
    {
        public GetRefundRequestsQueryValidator(PaginationOptions paginationOptions)
        {
            ArgumentNullException.ThrowIfNull(paginationOptions);

            var pageValidator = new OffsetPageRequestValidator(paginationOptions);

            RuleFor(query => query.BranchId)
                .Must(branchId => branchId is null || branchId != Guid.Empty)
                .WithMessage("The branch id cannot be empty.");

            RuleFor(query => query.AssignedUserId)
                .Must(userId => userId is null || userId != Guid.Empty)
                .WithMessage("The assigned user id cannot be empty.");

            RuleFor(query => query.BillId)
                .Must(billId => billId is null || billId != Guid.Empty)
                .WithMessage("The bill id cannot be empty.");

            RuleFor(query => query.Status)
                .Must(status => status is null || Enum.IsDefined(status.Value))
                .WithMessage("The status filter is not a known refund request status.");

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
