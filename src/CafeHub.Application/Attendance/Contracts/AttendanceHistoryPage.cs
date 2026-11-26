using CafeHub.Application.Attendance.DTOs;

namespace CafeHub.Application.Attendance.Contracts
{
    public sealed record AttendanceHistoryPage(
        IReadOnlyList<AttendanceHistoryItem> Items,
        int? TotalCount);
}
