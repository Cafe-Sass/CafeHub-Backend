namespace CafeHub.WebApi.Controllers
{
    public sealed record AttendanceDeviceEventRequest(
        string? EventId,
        string? Code,
        DateTimeOffset OccurredAt,
        string? Direction);

    public sealed record AttendanceDeviceEventsRequest(IReadOnlyList<AttendanceDeviceEventRequest>? Events);
}
