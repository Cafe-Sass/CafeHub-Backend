using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Wifi.Queries
{
    public sealed class GetIssuedWifiQueryValidator : AbstractValidator<GetIssuedWifiQuery>
    {
        public GetIssuedWifiQueryValidator(PaginationOptions paginationOptions)
        {
            ArgumentNullException.ThrowIfNull(paginationOptions);

            var pageValidator = new OffsetPageRequestValidator(paginationOptions);

            RuleFor(query => query.BranchId)
                .Must(branchId => branchId is null || branchId != Guid.Empty)
                .WithMessage("The branch id cannot be empty.");
            RuleFor(query => query.PackageId)
                .Must(packageId => packageId is null || packageId != Guid.Empty)
                .WithMessage("The package id cannot be empty.");
            RuleFor(query => query.OrderNumber)
                .GreaterThan(0)
                .When(query => query.OrderNumber.HasValue)
                .WithMessage("The order number must start at 1.");

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
