using FluentValidation;

namespace CafeHub.Application.Attendance.Queries
{
    public sealed class GetAttendanceDevicesQueryValidator : AbstractValidator<GetAttendanceDevicesQuery>
    {
        public GetAttendanceDevicesQueryValidator()
        {
            RuleFor(query => query.BranchId)
                .Must(branchId => branchId is null || branchId != Guid.Empty)
                .WithMessage("The branch id cannot be empty.");
        }
    }
}
