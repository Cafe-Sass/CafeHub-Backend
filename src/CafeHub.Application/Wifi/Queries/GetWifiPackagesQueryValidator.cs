using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Wifi.Queries
{
    public sealed class GetWifiPackagesQueryValidator : AbstractValidator<GetWifiPackagesQuery>
    {
        public GetWifiPackagesQueryValidator(PaginationOptions paginationOptions)
        {
            ArgumentNullException.ThrowIfNull(paginationOptions);

            var pageValidator = new OffsetPageRequestValidator(paginationOptions);

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
