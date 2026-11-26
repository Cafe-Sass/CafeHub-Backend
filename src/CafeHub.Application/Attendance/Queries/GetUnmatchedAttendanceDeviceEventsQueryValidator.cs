using FluentValidation;

namespace CafeHub.Application.Attendance.Queries
{
    public sealed class GetUnmatchedAttendanceDeviceEventsQueryValidator
        : AbstractValidator<GetUnmatchedAttendanceDeviceEventsQuery>
    {
        public GetUnmatchedAttendanceDeviceEventsQueryValidator()
        {
            RuleFor(query => query.BranchId)
                .Must(branchId => branchId is null || branchId != Guid.Empty)
                .WithMessage("The branch id cannot be empty.");
        }
    }
}
