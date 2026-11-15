using CafeHub.Application.Identity;
using FluentValidation;

namespace CafeHub.Application.Staff.Commands
{
    public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
    {

        public ChangePasswordCommandValidator()
        {
            RuleFor(command => command.CurrentPassword)
                .NotEmpty()
                .WithMessage("The current password is required.");

            RuleFor(command => command.NewPassword)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("The new password is required.")
                .Must(NewPasswordPolicy.IsValid)
                .WithMessage(NewPasswordPolicy.Requirements);
        }
    }
}
