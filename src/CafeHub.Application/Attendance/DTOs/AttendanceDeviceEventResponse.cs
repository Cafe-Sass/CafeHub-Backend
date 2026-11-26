namespace CafeHub.Application.Attendance.DTOs
{
    public sealed record AttendanceDeviceEventInput(
        string? EventId,
        string? Code,
        DateTimeOffset OccurredAt,
        string? Direction);

    public sealed record AttendanceDeviceEventOutcome(
        string EventId,
        string Outcome,
        Guid? RecordId,
        string? Reason);

    public sealed record AttendanceDeviceEventsResponse(
        IReadOnlyList<AttendanceDeviceEventOutcome> Outcomes);

    public static class AttendanceDeviceEventOutcomes
    {
        public const string Processed = "Processed";
        public const string Duplicate = "Duplicate";
        public const string Unmatched = "Unmatched";
        public const string Rejected = "Rejected";
    }

    public static class AttendanceDeviceEventReasons
    {
        public const string OutsideWindow = "OutsideWindow";
        public const string OutOfOrder = "OutOfOrder";
    }
}
