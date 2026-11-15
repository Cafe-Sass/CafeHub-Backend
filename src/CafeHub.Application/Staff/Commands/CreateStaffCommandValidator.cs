using FluentValidation;

namespace CafeHub.Application.Staff.Commands
{
    public sealed class CreateStaffCommandValidator : AbstractValidator<CreateStaffCommand>
    {
        public const int MaximumFullNameLength = 256;
        public const int MaximumEmailLength = 320;
        public const int MaximumPhoneNumberLength = 64;
        public const int MinimumPasswordLength = 6;

        public CreateStaffCommandValidator()
        {
            RuleFor(command => command.FullName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(name => !string.IsNullOrWhiteSpace(name))
                .WithMessage("The full name is required.")
                .MaximumLength(MaximumFullNameLength);

            RuleFor(command => command.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("The email is required.")
                .EmailAddress()
                .WithMessage("The email must be a valid email address.")
                .MaximumLength(MaximumEmailLength);

            RuleFor(command => command.PhoneNumber)
                .MaximumLength(MaximumPhoneNumberLength)
                .When(command => !string.IsNullOrWhiteSpace(command.PhoneNumber));

            RuleFor(command => command.BranchId)
                .NotEmpty()
                .WithMessage("The branch id is required.");

            RuleFor(command => command.Password)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("The password is required.")
                .MinimumLength(MinimumPasswordLength)
                .WithMessage($"The password must be at least {MinimumPasswordLength} characters.");
        }
    }
}
