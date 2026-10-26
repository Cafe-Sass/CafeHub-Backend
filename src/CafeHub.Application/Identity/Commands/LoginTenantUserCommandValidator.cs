using FluentValidation;

namespace CafeHub.Application.Identity.Commands
{
    public sealed class LoginTenantUserCommandValidator : AbstractValidator<LoginTenantUserCommand>
    {
        public LoginTenantUserCommandValidator()
        {
            RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(254);
            RuleFor(command => command.Password).NotEmpty().MaximumLength(128);
        }
    }
}
