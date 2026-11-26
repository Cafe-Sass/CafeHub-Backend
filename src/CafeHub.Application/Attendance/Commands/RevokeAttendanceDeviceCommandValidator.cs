using FluentValidation;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class RevokeAttendanceDeviceCommandValidator
        : AbstractValidator<RevokeAttendanceDeviceCommand>
    {
        public RevokeAttendanceDeviceCommandValidator()
        {
            RuleFor(command => command.DeviceId).NotEmpty()
                .WithMessage("The device id is required.");
            RuleFor(command => command.BranchId).NotEmpty()
                .WithMessage("The branch id is required.");
        }
    }
}
