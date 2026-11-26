namespace CafeHub.Application.Attendance.DTOs
{
    public sealed record AttendanceScanResponse(
        Guid RecordId,
        string Status,
        Guid BranchId,
        DateTimeOffset At)
    {
        public const string CheckedIn = "CheckedIn";

        public const string CheckedOut = "CheckedOut";
    }
}
