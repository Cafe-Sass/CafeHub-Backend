using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Pagination
{
    /// <summary>
    /// Validates one <see cref="SortRequest"/> against a list's <see cref="SortWhitelist"/>: the field must match a
    /// whitelisted field case-insensitively and the direction must be a defined sort direction. Every failure carries
    /// the field name <c>sort</c>. Each list request validator composes this validator through SetValidator with its
    /// own whitelist (proposed, pending owner confirmation: a container-wide whitelist is not registered because a
    /// whitelist belongs to one list).
    /// </summary>
    public sealed class SortRequestValidator : AbstractValidator<SortRequest>
    {
        public SortRequestValidator(SortWhitelist whitelist)
        {
            if (whitelist is null)
            {
                throw new ArgumentNullException(nameof(whitelist));
            }

            RuleFor(request => request)
                .Custom((request, context) =>
                {
                    if (!whitelist.Contains(request.Field))
                    {
                        var error = PaginationErrors.SortFieldIsNotAllowed(request.Field);
                        context.AddFailure(new ValidationFailure("sort", error.Message)
                        {
                            ErrorCode = PaginationErrors.SortFieldNotAllowedCode,
                        });
                    }

                    if (!Enum.IsDefined(request.Direction))
                    {
                        context.AddFailure(
                            new ValidationFailure("sort", PaginationErrors.SortDirectionIsInvalid().Message)
                            {
                                ErrorCode = PaginationErrors.SortDirectionInvalidCode,
                            });
                    }
                });
        }
    }
}
