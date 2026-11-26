using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class CreateAttendanceDeviceCommandValidator
        : AbstractValidator<CreateAttendanceDeviceCommand>
    {
        public CreateAttendanceDeviceCommandValidator()
        {
            RuleFor(command => command.BranchId).NotEmpty()
                .WithMessage("The branch id is required.");
            RuleFor(command => command.Name).Must(AttendanceDevice.IsValidName)
                .WithMessage(
                    $"The device name must be 1 to {AttendanceDevice.MaximumNameLength} characters without control characters.");
        }
    }
}
