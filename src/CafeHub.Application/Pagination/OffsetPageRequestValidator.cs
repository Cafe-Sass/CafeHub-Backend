using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Pagination
{
    public sealed class OffsetPageRequestValidator : AbstractValidator<OffsetPageRequest>
    {
        public OffsetPageRequestValidator(PaginationOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            RuleFor(request => request)
                .Custom((request, context) =>
                {
                    if (request.Page < 1)
                    {
                        context.AddFailure(new ValidationFailure("page", PaginationErrors.PageIsInvalid().Message)
                        {
                            ErrorCode = PaginationErrors.PageInvalidCode,
                        });
                    }

                    if (request.PageSize is not { } pageSize)
                    {
                        return;
                    }

                    if (pageSize < 1)
                    {
                        context.AddFailure(new ValidationFailure("pageSize", PaginationErrors.PageSizeIsInvalid().Message)
                        {
                            ErrorCode = PaginationErrors.PageSizeInvalidCode,
                        });
                    }

                    if (pageSize > options.MaxPageSize)
                    {
                        context.AddFailure(
                            new ValidationFailure("pageSize", PaginationErrors.PageSizeIsTooLarge(options.MaxPageSize).Message)
                            {
                                ErrorCode = PaginationErrors.PageSizeTooLargeCode,
                            });
                    }
                });
        }
    }
}
