using System.Text.RegularExpressions;
using FluentValidation;

namespace CafeHub.Application.Plans.Commands
{
    public sealed class CreatePlanCommandValidator : AbstractValidator<CreatePlanCommand>
    {
        public const int MinimumKeyLength = 3;
        public const int MaximumKeyLength = 64;
        public const int MaximumNameLength = 256;

        private const string KeyPattern = "^[a-z][a-z0-9-]{2,63}$";
        private static readonly Regex KeyRegex = new(KeyPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public CreatePlanCommandValidator()
        {
            RuleFor(command => command.Key)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(IsWellFormedKey)
                .WithMessage(
                    "The plan key must start with a lowercase letter and contain only lowercase letters, digits, and hyphens, between 3 and 64 characters.");

            RuleFor(command => command.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .MaximumLength(MaximumNameLength);
        }

        private static bool IsWellFormedKey(string? key)
        {
            return key is { Length: >= MinimumKeyLength and <= MaximumKeyLength } && KeyRegex.IsMatch(key);
        }
    }
}
