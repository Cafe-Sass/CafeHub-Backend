using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Attendance.DTOs;

namespace CafeHub.Application.Attendance.Queries
{
    public sealed record GetUnmatchedAttendanceDeviceEventsQuery(Guid? BranchId)
        : IQuery<IReadOnlyList<AttendanceUnmatchedEventResponse>>;
}
