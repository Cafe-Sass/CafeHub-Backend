using FluentValidation;

namespace CafeHub.Application.Staff.Commands
{
    public sealed class ResetStaffPasswordCommandValidator : AbstractValidator<ResetStaffPasswordCommand>
    {
        public const int MinimumPasswordLength = 6;

        public ResetStaffPasswordCommandValidator()
        {
            RuleFor(command => command.StaffId)
                .NotEmpty()
                .WithMessage("The staff id is required.");

            RuleFor(command => command.NewPassword)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("The new password is required.")
                .MinimumLength(MinimumPasswordLength)
                .WithMessage($"The new password must be at least {MinimumPasswordLength} characters.");
        }
    }
}
