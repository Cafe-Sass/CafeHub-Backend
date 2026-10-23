using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Pagination
{
    public sealed class KeysetPageRequestValidator : AbstractValidator<KeysetPageRequest>
    {
        public KeysetPageRequestValidator(PaginationOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            RuleFor(request => request)
                .Custom((request, context) =>
                {
                    if (request.Cursor is { } cursor && string.IsNullOrWhiteSpace(cursor))
                    {
                        context.AddFailure(new ValidationFailure("cursor", "A cursor, when provided, must not be empty.")
                        {
                            ErrorCode = PaginationErrors.CursorInvalidCode,
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
