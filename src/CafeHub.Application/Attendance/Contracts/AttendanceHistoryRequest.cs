namespace CafeHub.Application.Attendance.Contracts
{
    public sealed record AttendanceHistoryRequest(
        int Page,
        int PageSize,
        bool IncludeTotalCount,
        Guid BranchId,
        Guid? StaffId,
        DateOnly? From,
        DateOnly? To);
}
