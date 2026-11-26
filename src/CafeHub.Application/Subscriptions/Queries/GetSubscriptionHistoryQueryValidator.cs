using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Subscriptions.Queries
{
    public sealed class GetSubscriptionHistoryQueryValidator
        : AbstractValidator<GetSubscriptionHistoryQuery>
    {
        public GetSubscriptionHistoryQueryValidator(PaginationOptions paginationOptions)
        {
            ArgumentNullException.ThrowIfNull(paginationOptions);

            var pageValidator = new OffsetPageRequestValidator(paginationOptions);
            RuleFor(query => query).Custom((query, context) =>
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

                var pageSize = query.PageSize ?? paginationOptions.DefaultPageSize;
                if (query.Page >= 1
                    && pageSize >= 1
                    && (long)(query.Page - 1) * pageSize > int.MaxValue)
                {
                    context.AddFailure(new ValidationFailure(
                        "page", "The requested page offset is too large.")
                    {
                        ErrorCode = PaginationErrors.PageInvalidCode,
                    });
                }
            });
        }
    }
}
