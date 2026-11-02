using System.Text.RegularExpressions;
using FluentValidation;

namespace CafeHub.Application.CafeProfile.Commands
{
    public sealed class UpdateCafeProfileCommandValidator : AbstractValidator<UpdateCafeProfileCommand>
    {
        public const int MaximumNameLength = 256;
        public const int MaximumLogoMediaReferenceLength = 512;
        public const int MaximumPhoneLength = 32;
        public const int MaximumEmailLength = 320;
        public const int MaximumAddressLength = 512;
        public const int MaximumTimezoneLength = 64;

        private static readonly Regex ColorPattern = new(
            "^#[0-9a-fA-F]{6}$",
            RegexOptions.CultureInvariant);

        private static readonly Regex PhonePattern = new(
            "^[0-9()\\s+-]{3,32}$",
            RegexOptions.CultureInvariant);

        public UpdateCafeProfileCommandValidator()
        {
            RuleFor(command => command.CafeName)
                .NotEmpty()
                .MaximumLength(MaximumNameLength);

            RuleFor(command => command.LogoMediaReference)
                .MaximumLength(MaximumLogoMediaReferenceLength)
                .When(command => !string.IsNullOrWhiteSpace(command.LogoMediaReference));

            RuleFor(command => command.PrimaryColor)
                .Must(BeWellFormedColor)
                .When(command => !string.IsNullOrWhiteSpace(command.PrimaryColor))
                .WithMessage("The primary color must be a six-digit hex color code prefixed with '#'.");

            RuleFor(command => command.SecondaryColor)
                .Must(BeWellFormedColor)
                .When(command => !string.IsNullOrWhiteSpace(command.SecondaryColor))
                .WithMessage("The secondary color must be a six-digit hex color code prefixed with '#'.");

            RuleFor(command => command.AccentColor)
                .Must(BeWellFormedColor)
                .When(command => !string.IsNullOrWhiteSpace(command.AccentColor))
                .WithMessage("The accent color must be a six-digit hex color code prefixed with '#'.");

            RuleFor(command => command.ContactPhone)
                .Matches(PhonePattern)
                .When(command => !string.IsNullOrWhiteSpace(command.ContactPhone))
                .WithMessage("The contact phone must contain only digits, spaces, plus, hyphen, and parentheses.");

            RuleFor(command => command.ContactEmail)
                .EmailAddress()
                .MaximumLength(MaximumEmailLength)
                .When(command => !string.IsNullOrWhiteSpace(command.ContactEmail));

            RuleFor(command => command.ContactAddress)
                .MaximumLength(MaximumAddressLength)
                .When(command => !string.IsNullOrWhiteSpace(command.ContactAddress));

            RuleFor(command => command.Timezone)
                .NotEmpty()
                .MaximumLength(MaximumTimezoneLength)
                .Must(BeValidTimeZone)
                .WithMessage("The time zone is not a valid IANA identifier.");

            RuleFor(command => command.ConcurrencyToken)
                .NotEmpty()
                .Must(BeValidConcurrencyToken)
                .WithMessage("The concurrency token is not a valid unsigned integer.");
        }

        private static bool BeWellFormedColor(string? color)
        {
            return color is null || ColorPattern.IsMatch(color);
        }

        private static bool BeValidTimeZone(string timezone)
        {
            return TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out _);
        }

        private static bool BeValidConcurrencyToken(string? token)
        {
            return uint.TryParse(token, out _);
        }
    }
}
