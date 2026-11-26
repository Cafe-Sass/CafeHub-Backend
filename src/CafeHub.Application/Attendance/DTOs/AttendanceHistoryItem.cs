namespace CafeHub.Application.Attendance.DTOs
{
    public sealed record AttendanceHistoryItem(
        Guid Id,
        Guid StaffId,
        string StaffName,
        Guid BranchId,
        string BranchName,
        DateTimeOffset CheckInAt,
        DateTimeOffset? CheckOutAt,
        Guid? CheckInSessionId,
        Guid? CheckOutSessionId,
        string Source);
}
