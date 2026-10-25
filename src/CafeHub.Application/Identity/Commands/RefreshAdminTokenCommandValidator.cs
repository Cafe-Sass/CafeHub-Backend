using FluentValidation;

namespace CafeHub.Application.Identity.Commands
{
    public sealed class RefreshAdminTokenCommandValidator : AbstractValidator<RefreshAdminTokenCommand>
    {
        public RefreshAdminTokenCommandValidator()
        {
            RuleFor(command => command.RefreshToken)
                .NotEmpty()
                .MaximumLength(IRefreshTokenService.MaxRawTokenLength);
        }
    }
}
