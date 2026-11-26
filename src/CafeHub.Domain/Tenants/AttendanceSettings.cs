namespace CafeHub.Domain.Tenants
{
    public sealed class AttendanceSettings
    {
        private AttendanceSettings()
        {
        }

        public Guid Id { get; private set; }

        public AttendanceMode Mode { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static AttendanceSettings Create(Guid id, DateTimeOffset now, Guid? actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The attendance settings id is required.", nameof(id));
            }

            return new AttendanceSettings
            {
                Id = id,
                Mode = AttendanceMode.Off,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        public bool Update(AttendanceMode mode, DateTimeOffset now, Guid? actorId)
        {
            if (!IsValidMode(mode))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(mode), "The attendance mode is not defined.");
            }

            if (Mode == mode)
            {
                return false;
            }

            Mode = mode;
            UpdatedAt = now;
            UpdatedBy = actorId;
            return true;
        }

        public static bool IsValidMode(AttendanceMode mode) => Enum.IsDefined(mode);

        public bool AllowsQrSessions => Mode == AttendanceMode.Qr;
    }
}
