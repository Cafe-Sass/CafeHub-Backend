namespace CafeHub.Domain.Tenants
{
    public sealed class AttendanceRecord
    {
        private AttendanceRecord()
        {
        }

        public Guid Id { get; private set; }

        public Guid BranchId { get; private set; }

        public Guid StaffId { get; private set; }

        public DateTimeOffset CheckInAt { get; private set; }

        public DateTimeOffset? CheckOutAt { get; private set; }

        public Guid? CheckInSessionId { get; private set; }

        public Guid? CheckOutSessionId { get; private set; }

        public AttendanceSource Source { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public bool IsOpen => CheckOutAt is null;

        public static AttendanceRecord CheckIn(
            Guid id,
            Guid branchId,
            Guid staffId,
            Guid sessionId,
            AttendanceSource source,
            DateTimeOffset now)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The attendance record id is required.", nameof(id));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            if (staffId == Guid.Empty)
            {
                throw new ArgumentException("The staff id is required.", nameof(staffId));
            }

            if (sessionId == Guid.Empty)
            {
                throw new ArgumentException("The attendance session id is required.", nameof(sessionId));
            }

            if (!Enum.IsDefined(source))
            {
                throw new ArgumentOutOfRangeException(nameof(source), "The attendance source is not defined.");
            }

            return new AttendanceRecord
            {
                Id = id,
                BranchId = branchId,
                StaffId = staffId,
                CheckInAt = now,
                CheckInSessionId = sessionId,
                Source = source,
                CreatedAt = now,
                UpdatedAt = now,
            };
        }

        public bool CanCheckOutWith(Guid sessionId)
        {
            if (!IsOpen || Source != AttendanceSource.Qr)
            {
                return false;
            }

            return CheckInSessionId != sessionId
                && (CheckOutSessionId is null || CheckOutSessionId != sessionId);
        }

        public void CheckOut(Guid sessionId, DateTimeOffset now)
        {
            if (sessionId == Guid.Empty)
            {
                throw new ArgumentException("The attendance session id is required.", nameof(sessionId));
            }

            if (!IsOpen)
            {
                throw new InvalidOperationException("The attendance record is already closed.");
            }

            if (!CanCheckOutWith(sessionId))
            {
                throw new InvalidOperationException(
                    "The attendance session cannot close this record.");
            }

            if (now <= CheckInAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(now), "The check-out time must be after the check-in time.");
            }

            CheckOutSessionId = sessionId;
            CheckOutAt = now;
            UpdatedAt = now;
        }

        public static AttendanceRecord CheckInFromDevice(
            Guid id,
            Guid branchId,
            Guid staffId,
            DateTimeOffset occurredAt,
            DateTimeOffset now)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The attendance record id is required.", nameof(id));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            if (staffId == Guid.Empty)
            {
                throw new ArgumentException("The staff id is required.", nameof(staffId));
            }

            return new AttendanceRecord
            {
                Id = id,
                BranchId = branchId,
                StaffId = staffId,
                CheckInAt = occurredAt,
                Source = AttendanceSource.Device,
                CreatedAt = now,
                UpdatedAt = now,
            };
        }

        public void CheckOutFromDevice(DateTimeOffset occurredAt, DateTimeOffset now)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("The attendance record is already closed.");
            }

            if (occurredAt <= CheckInAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(occurredAt), "The check-out time must be after the check-in time.");
            }

            CheckOutAt = occurredAt;
            UpdatedAt = now;
        }
    }
}
