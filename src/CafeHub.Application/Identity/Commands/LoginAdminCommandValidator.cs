using FluentValidation;

namespace CafeHub.Application.Identity.Commands
{
    public sealed class LoginAdminCommandValidator : AbstractValidator<LoginAdminCommand>
    {
        public const int MaxEmailLength = 254;
        public const int MaxPasswordLength = 128;

        public LoginAdminCommandValidator()
        {
            RuleFor(command => command.Email)
                .NotEmpty()
                .MaximumLength(MaxEmailLength)
                .EmailAddress();

            RuleFor(command => command.Password)
                .NotEmpty()
                .MaximumLength(MaxPasswordLength);
        }
    }
}
