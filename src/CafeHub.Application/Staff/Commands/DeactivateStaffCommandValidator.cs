using FluentValidation;

namespace CafeHub.Application.Staff.Commands
{
    public sealed class DeactivateStaffCommandValidator : AbstractValidator<DeactivateStaffCommand>
    {
        public DeactivateStaffCommandValidator()
        {
            RuleFor(command => command.StaffId)
                .NotEmpty()
                .WithMessage("The staff id is required.");
        }
    }
}
