using System.Text.RegularExpressions;
using FluentValidation;

namespace CafeHub.Application.Onboarding.Commands
{
    public sealed class RegisterOwnerCommandValidator : AbstractValidator<RegisterOwnerCommand>
    {
        public const int MinimumPasswordLength = 12;
        public const int MaximumPasswordLength = 128;
        public const int MaximumEmailLength = 254;
        public const int MaximumNameLength = 256;
        public const int MaximumPhoneLength = 64;

        private static readonly Regex PhonePattern = new(
            "^[0-9][0-9()\\s+-]{0,62}$",
            RegexOptions.CultureInvariant);

        public RegisterOwnerCommandValidator()
        {
            RuleFor(command => command.OwnerEmail)
                .NotEmpty()
                .MaximumLength(MaximumEmailLength)
                .EmailAddress();

            RuleFor(command => command.OwnerFullName)
                .NotEmpty()
                .MaximumLength(MaximumNameLength);

            RuleFor(command => command.CafeName)
                .NotEmpty()
                .MaximumLength(MaximumNameLength);

            RuleFor(command => command.OwnerPhone)
                .MaximumLength(MaximumPhoneLength)
                .When(command => command.OwnerPhone is not null)
                .Matches(PhonePattern)
                .When(command => !string.IsNullOrWhiteSpace(command.OwnerPhone));

            RuleFor(command => command.Password)
                .NotEmpty()
                .MinimumLength(MinimumPasswordLength)
                .MaximumLength(MaximumPasswordLength);

            RuleFor(command => command.Slug)
                .NotEmpty();

            RuleFor(command => command.PlanVersionId)
                .NotEmpty();
        }
    }
}
