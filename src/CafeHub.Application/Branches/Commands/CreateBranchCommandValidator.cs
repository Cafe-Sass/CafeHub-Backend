using FluentValidation;

namespace CafeHub.Application.Branches.Commands
{
    public sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
    {
        public const int MaximumNameLength = 256;
        public const int MaximumAddressLength = 512;
        public const int MaximumContactPhoneLength = 32;
        public const int MaximumOpeningHoursSummaryLength = 512;

        public CreateBranchCommandValidator()
        {
            RuleFor(command => command.Name)
                .NotEmpty()
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
        }
    }
}
