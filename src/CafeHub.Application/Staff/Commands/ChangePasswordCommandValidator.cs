using FluentValidation;

namespace CafeHub.Application.Staff.Commands
{
    public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
    {
        public const int MinimumPasswordLength = 6;

        public ChangePasswordCommandValidator()
        {
            RuleFor(command => command.CurrentPassword)
                .NotEmpty()
                .WithMessage("The current password is required.");

            RuleFor(command => command.NewPassword)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("The new password is required.")
                .MinimumLength(MinimumPasswordLength)
                .WithMessage($"The new password must be at least {MinimumPasswordLength} characters.");
        }
    }
}
