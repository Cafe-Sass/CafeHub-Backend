using FluentValidation;

namespace CafeHub.Application.Attendance.Commands
{
    public sealed class ReprocessUnmatchedAttendanceDeviceEventsCommandValidator
        : AbstractValidator<ReprocessUnmatchedAttendanceDeviceEventsCommand>
    {
        public ReprocessUnmatchedAttendanceDeviceEventsCommandValidator()
        {
            RuleFor(command => command.BranchId).NotEmpty()
                .WithMessage("The branch id is required.");
        }
    }
}
