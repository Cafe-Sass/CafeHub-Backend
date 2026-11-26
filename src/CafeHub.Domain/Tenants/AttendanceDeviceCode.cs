namespace CafeHub.Domain.Tenants
{
    public sealed class AttendanceDeviceCode
    {
        public const int MaximumCodeLength = 64;

        private AttendanceDeviceCode()
        {
        }

        public Guid Id { get; private set; }

        public Guid BranchId { get; private set; }

        public Guid StaffId { get; private set; }

        public string DeviceCode { get; private set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static AttendanceDeviceCode Create(
            Guid id,
            Guid branchId,
            Guid staffId,
            string deviceCode,
            DateTimeOffset now,
            Guid? actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The attendance device code id is required.", nameof(id));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The attendance device code branch id is required.", nameof(branchId));
            }

            if (staffId == Guid.Empty)
            {
                throw new ArgumentException("The attendance device code staff id is required.", nameof(staffId));
            }

            if (!IsValidCode(deviceCode))
            {
                throw new ArgumentException(
                    $"The device code must be 1 to {MaximumCodeLength} characters without control characters.",
                    nameof(deviceCode));
            }

            return new AttendanceDeviceCode
            {
                Id = id,
                BranchId = branchId,
                StaffId = staffId,
                DeviceCode = deviceCode.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        public bool ChangeCode(string deviceCode, DateTimeOffset now, Guid? actorId)
        {
            if (!IsValidCode(deviceCode))
            {
                throw new ArgumentException(
                    $"The device code must be 1 to {MaximumCodeLength} characters without control characters.",
                    nameof(deviceCode));
            }

            var trimmed = deviceCode.Trim();
            if (string.Equals(DeviceCode, trimmed, StringComparison.Ordinal))
            {
                return false;
            }

            DeviceCode = trimmed;
            UpdatedAt = now;
            UpdatedBy = actorId;
            return true;
        }

        public static bool IsValidCode(string? deviceCode)
        {
            var trimmed = deviceCode?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length <= MaximumCodeLength
                && !trimmed.Any(char.IsControl);
        }
    }
}
