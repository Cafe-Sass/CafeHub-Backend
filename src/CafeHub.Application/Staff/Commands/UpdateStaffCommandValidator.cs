using FluentValidation;

namespace CafeHub.Application.Staff.Commands
{
    public sealed class UpdateStaffCommandValidator : AbstractValidator<UpdateStaffCommand>
    {
        public const int MaximumFullNameLength = 256;
        public const int MaximumPhoneNumberLength = 64;

        public UpdateStaffCommandValidator()
        {
            RuleFor(command => command.StaffId)
                .NotEmpty()
                .WithMessage("The staff id is required.");

            RuleFor(command => command.FullName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(name => !string.IsNullOrWhiteSpace(name))
                .WithMessage("The full name is required.")
                .MaximumLength(MaximumFullNameLength);

            RuleFor(command => command.PhoneNumber)
                .MaximumLength(MaximumPhoneNumberLength)
                .When(command => !string.IsNullOrWhiteSpace(command.PhoneNumber));

            RuleFor(command => command.BranchId)
                .NotEmpty()
                .WithMessage("The branch id is required.");

            RuleFor(command => command.ConcurrencyToken)
                .NotEmpty()
                .WithMessage("The concurrency token is required.");
        }
    }
}
