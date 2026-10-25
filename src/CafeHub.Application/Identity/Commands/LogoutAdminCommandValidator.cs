using FluentValidation;

namespace CafeHub.Application.Identity.Commands
{
    public sealed class LogoutAdminCommandValidator : AbstractValidator<LogoutAdminCommand>
    {
        public LogoutAdminCommandValidator()
        {
            RuleFor(command => command.RefreshToken)
                .NotEmpty()
                .MaximumLength(IRefreshTokenService.MaxRawTokenLength);
        }
    }
}
