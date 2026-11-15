using FluentValidation;

namespace CafeHub.Application.Staff.Commands
{
    public sealed class ReactivateStaffCommandValidator : AbstractValidator<ReactivateStaffCommand>
    {
        public ReactivateStaffCommandValidator()
        {
            RuleFor(command => command.StaffId)
                .NotEmpty()
                .WithMessage("The staff id is required.");
        }
    }
}
