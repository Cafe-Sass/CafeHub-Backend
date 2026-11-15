using CafeHub.Application.Identity;
using FluentValidation;

namespace CafeHub.Application.Staff.Commands
{
    public sealed class ResetStaffPasswordCommandValidator : AbstractValidator<ResetStaffPasswordCommand>
    {

        public ResetStaffPasswordCommandValidator()
        {
            RuleFor(command => command.StaffId)
                .NotEmpty()
                .WithMessage("The staff id is required.");

            RuleFor(command => command.NewPassword)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("The new password is required.")
                .Must(NewPasswordPolicy.IsValid)
                .WithMessage(NewPasswordPolicy.Requirements);
        }
    }
}
