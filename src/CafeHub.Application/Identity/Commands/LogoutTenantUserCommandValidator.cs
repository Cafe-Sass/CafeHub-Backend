using FluentValidation;

namespace CafeHub.Application.Identity.Commands
{
    public sealed class LogoutTenantUserCommandValidator : AbstractValidator<LogoutTenantUserCommand>
    {
        public LogoutTenantUserCommandValidator()
        {
            RuleFor(command => command.RefreshToken)
                .NotEmpty()
                .MaximumLength(ITenantRefreshTokenService.MaxRawTokenLength);
        }
    }
}
