namespace CafeHub.Domain.Tenants
{
    public sealed class PrintConnector
    {
        public const int MaximumNameLength = 64;
        public const int KeyHashLength = 64;

        private PrintConnector()
        {
        }

        public Guid Id { get; private set; }

        public Guid BranchId { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public string KeyHash { get; private set; } = string.Empty;

        public PrintConnectorStatus Status { get; private set; }

        public DateTimeOffset? LastSeenAt { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static PrintConnector Create(
            Guid id,
            Guid branchId,
            string name,
            string keyHash,
            DateTimeOffset now,
            Guid? actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The print connector id is required.", nameof(id));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            if (!IsValidName(name))
            {
                throw new ArgumentException(
                    $"The connector name must be 1 to {MaximumNameLength} characters without control characters.",
                    nameof(name));
            }

            if (!IsValidKeyHash(keyHash))
            {
                throw new ArgumentException(
                    $"The connector key hash must be {KeyHashLength} hexadecimal characters.",
                    nameof(keyHash));
            }

            return new PrintConnector
            {
                Id = id,
                BranchId = branchId,
                Name = name.Trim(),
                KeyHash = keyHash,
                Status = PrintConnectorStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        public bool Revoke(DateTimeOffset now, Guid? actorId)
        {
            if (Status == PrintConnectorStatus.Revoked)
            {
                return false;
            }

            Status = PrintConnectorStatus.Revoked;
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
