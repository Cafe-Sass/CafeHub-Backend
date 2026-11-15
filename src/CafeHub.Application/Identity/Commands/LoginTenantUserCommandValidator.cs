using FluentValidation;

namespace CafeHub.Application.Identity.Commands
{
    public sealed class LoginTenantUserCommandValidator : AbstractValidator<LoginTenantUserCommand>
    {
        public const int MaximumEmailLength = 254;

        public LoginTenantUserCommandValidator()
        {
            RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(MaximumEmailLength);
            RuleFor(command => command.Password).NotEmpty().MaximumLength(128);
        }
    }
}
