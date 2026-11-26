namespace CafeHub.Domain.Tenants
{
    public sealed class AttendanceDeviceEvent
    {
        public const int MaximumEventIdLength = 128;

        private AttendanceDeviceEvent()
        {
        }

        public Guid Id { get; private set; }

        public Guid DeviceId { get; private set; }

        public string DeviceEventId { get; private set; } = string.Empty;

        public string DeviceCode { get; private set; } = string.Empty;

        public DateTimeOffset OccurredAt { get; private set; }

        public AttendanceDeviceEventDirection? Direction { get; private set; }

        public AttendanceDeviceEventStatus Status { get; private set; }

        public Guid? RecordId { get; private set; }

        public DateTimeOffset ReceivedAt { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public static AttendanceDeviceEvent Create(
            Guid id,
            Guid deviceId,
            string deviceEventId,
            string deviceCode,
            DateTimeOffset occurredAt,
            AttendanceDeviceEventDirection? direction,
            DateTimeOffset now)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The attendance device event id is required.", nameof(id));
            }

            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException("The attendance device id is required.", nameof(deviceId));
            }

            if (!IsValidEventId(deviceEventId))
            {
                throw new ArgumentException(
                    $"The device event id must be 1 to {MaximumEventIdLength} characters without control characters.",
                    nameof(deviceEventId));
            }

            if (!AttendanceDeviceCode.IsValidCode(deviceCode))
            {
                throw new ArgumentException(
                    $"The device code must be 1 to {AttendanceDeviceCode.MaximumCodeLength} characters without control characters.",
                    nameof(deviceCode));
            }

            if (direction is { } value && !Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(direction), "The device event direction is not defined.");
            }

            return new AttendanceDeviceEvent
            {
                Id = id,
                DeviceId = deviceId,
                DeviceEventId = deviceEventId.Trim(),
                DeviceCode = deviceCode.Trim(),
                OccurredAt = occurredAt,
                Direction = direction,
                Status = AttendanceDeviceEventStatus.Unmatched,
                ReceivedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            };
        }

        public bool MarkProcessed(Guid recordId, DateTimeOffset now)
        {
            if (recordId == Guid.Empty)
            {
                throw new ArgumentException("The attendance record id is required.", nameof(recordId));
            }

            if (Status == AttendanceDeviceEventStatus.Processed)
            {
                return false;
            }

            RecordId = recordId;
            Status = AttendanceDeviceEventStatus.Processed;
            UpdatedAt = now;
            return true;
        }

        public static bool IsValidEventId(string? eventId)
        {
            var trimmed = eventId?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length <= MaximumEventIdLength
                && !trimmed.Any(char.IsControl);
        }

        public static bool IsValidDirection(AttendanceDeviceEventDirection? direction) =>
            direction is null || Enum.IsDefined(direction.Value);
    }
}
