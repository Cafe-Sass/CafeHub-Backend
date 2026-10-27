using FluentValidation;

namespace CafeHub.Application.Identity.Commands
{
    public sealed class RefreshTenantTokenCommandValidator : AbstractValidator<RefreshTenantTokenCommand>
    {
        public RefreshTenantTokenCommandValidator()
        {
            RuleFor(command => command.RefreshToken)
                .NotEmpty()
                .MaximumLength(ITenantRefreshTokenService.MaxRawTokenLength);
        }
    }
}
