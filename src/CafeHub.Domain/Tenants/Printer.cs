namespace CafeHub.Domain.Tenants
{
    public sealed class Printer
    {
        public const int MaximumNameLength = 64;
        public const int MaximumConnectionKindLength = 32;

        private Printer()
        {
        }

        public Guid Id { get; private set; }

        public Guid BranchId { get; private set; }

        public Guid? StationId { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public PrinterProfile Profile { get; private set; }

        public string ConnectionKind { get; private set; } = string.Empty;

        public PrinterStatus Status { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static Printer Create(
            Guid id,
            Guid branchId,
            Guid? stationId,
            string name,
            PrinterProfile profile,
            string connectionKind,
            DateTimeOffset now,
            Guid? actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The printer id is required.", nameof(id));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            var printer = new Printer
            {
                Id = id,
                BranchId = branchId,
                Status = PrinterStatus.Active,
                CreatedAt = now,
                CreatedBy = actorId,
            };
            printer.Apply(stationId, name, profile, connectionKind, now, actorId);
            return printer;
        }

        public bool Update(
            Guid? stationId,
            string name,
            PrinterProfile profile,
            string connectionKind,
            DateTimeOffset now,
            Guid? actorId)
        {
            if (StationId == stationId
                && Name == name?.Trim()
                && Profile == profile
                && ConnectionKind == connectionKind?.Trim())
            {
                return false;
            }

            Apply(stationId, name, profile, connectionKind, now, actorId);
            return true;
        }

        public bool Deactivate(DateTimeOffset now, Guid? actorId) =>
            ChangeStatus(PrinterStatus.Inactive, now, actorId);

        public bool Reactivate(DateTimeOffset now, Guid? actorId) =>
            ChangeStatus(PrinterStatus.Active, now, actorId);

        public static bool IsValidName(string? name)
        {
            var trimmed = name?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length <= MaximumNameLength
                && !trimmed.Any(char.IsControl);
        }

        public static bool IsValidConnectionKind(string? connectionKind)
        {
            var trimmed = connectionKind?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length <= MaximumConnectionKindLength
                && !trimmed.Any(char.IsControl);
        }

        private bool ChangeStatus(PrinterStatus status, DateTimeOffset now, Guid? actorId)
        {
            if (!Enum.IsDefined(status))
            {
                throw new ArgumentOutOfRangeException(nameof(status), "The printer status is not defined.");
            }

            if (Status == status)
            {
                return false;
            }

            Status = status;
            UpdatedAt = now;
            UpdatedBy = actorId;
            return true;
        }

        private void Apply(
            Guid? stationId,
            string name,
            PrinterProfile profile,
            string connectionKind,
            DateTimeOffset now,
            Guid? actorId)
        {
            if (stationId == Guid.Empty)
            {
                throw new ArgumentException("The station id cannot be empty.", nameof(stationId));
            }

            if (!IsValidName(name))
            {
                throw new ArgumentException(
                    $"The printer name must be 1 to {MaximumNameLength} characters without control characters.",
                    nameof(name));
            }

            if (!Enum.IsDefined(profile) || !PrinterProfiles.TryGet(profile, out _))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(profile),
                    "The printer profile is not in the supported profile set.");
            }

            if (!IsValidConnectionKind(connectionKind))
            {
                throw new ArgumentException(
                    $"The connection kind must be 1 to {MaximumConnectionKindLength} characters without control characters.",
                    nameof(connectionKind));
            }

            StationId = stationId;
            Name = name.Trim();
            Profile = profile;
            ConnectionKind = connectionKind.Trim();
            UpdatedAt = now;
            UpdatedBy = actorId;
        }
    }
}
