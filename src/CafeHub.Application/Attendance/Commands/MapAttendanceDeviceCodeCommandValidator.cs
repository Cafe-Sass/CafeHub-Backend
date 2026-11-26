using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class MapAttendanceDeviceCodeCommandValidator
        : AbstractValidator<MapAttendanceDeviceCodeCommand>
    {
        public MapAttendanceDeviceCodeCommandValidator()
        {
            RuleFor(command => command.BranchId).NotEmpty()
                .WithMessage("The branch id is required.");
            RuleFor(command => command.StaffId).NotEmpty()
                .WithMessage("The staff id is required.");
            RuleFor(command => command.DeviceCode).Must(AttendanceDeviceCode.IsValidCode)
                .WithMessage(
                    $"The device code must be 1 to {AttendanceDeviceCode.MaximumCodeLength} characters without control characters.");
        }
    }
}
