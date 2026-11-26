namespace CafeHub.Application.Attendance.DTOs
{
    public sealed record AttendanceUnmatchedEventResponse(
        Guid Id,
        Guid DeviceId,
        string DeviceName,
        string DeviceCode,
        DateTimeOffset OccurredAt,
        string? Direction,
        DateTimeOffset ReceivedAt);

    public sealed record AttendanceDeviceReprocessResponse(
        int Processed,
        int Unmatched,
        int Rejected);
}
