using FluentValidation;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class UnmapAttendanceDeviceCodeCommandValidator
        : AbstractValidator<UnmapAttendanceDeviceCodeCommand>
    {
        public UnmapAttendanceDeviceCodeCommandValidator()
        {
            RuleFor(command => command.BranchId).NotEmpty()
                .WithMessage("The branch id is required.");
            RuleFor(command => command.StaffId).NotEmpty()
                .WithMessage("The staff id is required.");
        }
    }
}
