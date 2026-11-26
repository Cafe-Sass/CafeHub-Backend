using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Attendance.Queries
{
    public sealed record GetAttendanceHistoryQuery : IQuery<OffsetPage<AttendanceHistoryItem>>
    {
        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public Guid? BranchId { get; init; }

        public Guid? StaffId { get; init; }

        public DateOnly? From { get; init; }

        public DateOnly? To { get; init; }
    }
}
