using CafeHub.Application.Attendance.DTOs;

namespace CafeHub.Application.Attendance
{
    public sealed record AttendanceDeviceEventBatchItem(
        int Index,
        AttendanceDeviceEventInput Input,
        bool IsDuplicate,
        bool IsOutsideWindow);

    public static class AttendanceDeviceEventBatch
    {
        public static IReadOnlyList<AttendanceDeviceEventBatchItem> Classify(
            IReadOnlyList<AttendanceDeviceEventInput> events,
            IReadOnlySet<string> existingEventIds,
            DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(events);
            ArgumentNullException.ThrowIfNull(existingEventIds);

            var items = new List<AttendanceDeviceEventBatchItem>(events.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < events.Count; index++)
            {
                var input = events[index];
                var eventId = input.EventId!.Trim();
                var duplicate = existingEventIds.Contains(eventId) || !seen.Add(eventId);
                var outsideWindow = !duplicate
                    && !AttendanceDeviceEventPolicy.IsWithinWindow(input.OccurredAt, now);
                items.Add(new AttendanceDeviceEventBatchItem(
                    index, input, duplicate, outsideWindow));
            }

            return items;
        }
    }
}
