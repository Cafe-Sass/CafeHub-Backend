using System.Text.RegularExpressions;
using FluentValidation;

namespace CafeHub.Application.Branches.Commands
{
    public sealed class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
    {
        public const int MaximumNameLength = 256;
        public const int MaximumAddressLength = 512;
        public const int MaximumContactPhoneLength = 32;
        public const int MaximumOpeningHoursSummaryLength = 512;

        public UpdateBranchCommandValidator()
        {
            RuleFor(command => command.BranchId)
                .NotEmpty();

            RuleFor(command => command.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(name => !string.IsNullOrWhiteSpace(name))
                .WithMessage("The branch name is required.")
                .MaximumLength(MaximumNameLength);

            RuleFor(command => command.Address)
                .MaximumLength(MaximumAddressLength)
                .When(command => !string.IsNullOrWhiteSpace(command.Address));

            RuleFor(command => command.ContactPhone)
                .MaximumLength(MaximumContactPhoneLength)
                .When(command => !string.IsNullOrWhiteSpace(command.ContactPhone));

            RuleFor(command => command.OpeningHoursSummary)
                .MaximumLength(MaximumOpeningHoursSummaryLength)
                .When(command => !string.IsNullOrWhiteSpace(command.OpeningHoursSummary));

            RuleFor(command => command.ConcurrencyToken)
                .NotEmpty()
                .Must(BeValidConcurrencyToken)
                .WithMessage("The concurrency token is not a valid unsigned integer.");
        }

        private static bool BeValidConcurrencyToken(string? token)
        {
            return uint.TryParse(token, out _);
        }
    }
}
