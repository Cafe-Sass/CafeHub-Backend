namespace CafeHub.Domain.Tenants
{
    public sealed class AttendanceDevice
    {
        public const int MaximumNameLength = 64;
        public const int KeyHashLength = 64;

        private AttendanceDevice()
        {
        }

        public Guid Id { get; private set; }

        public Guid BranchId { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public string KeyHash { get; private set; } = string.Empty;

        public AttendanceDeviceStatus Status { get; private set; }

        public DateTimeOffset? LastSeenAt { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static AttendanceDevice Create(
            Guid id,
            Guid branchId,
            string name,
            string keyHash,
            DateTimeOffset now,
            Guid? actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The attendance device id is required.", nameof(id));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The attendance device branch id is required.", nameof(branchId));
            }

            if (!IsValidName(name))
            {
                throw new ArgumentException(
                    $"The device name must be 1 to {MaximumNameLength} characters without control characters.",
                    nameof(name));
            }

            if (!IsValidKeyHash(keyHash))
            {
                throw new ArgumentException(
                    $"The device key hash must be {KeyHashLength} hexadecimal characters.",
                    nameof(keyHash));
            }

            return new AttendanceDevice
            {
                Id = id,
                BranchId = branchId,
                Name = name.Trim(),
                KeyHash = keyHash,
                Status = AttendanceDeviceStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        public bool Revoke(DateTimeOffset now, Guid? actorId)
        {
            if (Status == AttendanceDeviceStatus.Revoked)
            {
                return false;
            }

            Status = AttendanceDeviceStatus.Revoked;
            UpdatedAt = now;
            UpdatedBy = actorId;
            return true;
        }

        public void MarkSeen(DateTimeOffset now) => LastSeenAt = now;

        public static bool IsValidName(string? name)
        {
            var trimmed = name?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length <= MaximumNameLength
                && !trimmed.Any(char.IsControl);
        }

        public static bool IsValidKeyHash(string? keyHash) =>
            keyHash is { Length: KeyHashLength } && keyHash.All(char.IsAsciiHexDigit);
    }
}
