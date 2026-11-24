namespace CafeHub.Domain.Tenants
{
    public sealed class WifiVoucher
    {
        public const int MinimumCodeLength = 4;
        public const int MaximumCodeLength = 64;

        private WifiVoucher()
        {
        }

        public Guid Id { get; private set; }

        public Guid PackageId { get; private set; }

        public string Code { get; private set; } = string.Empty;

        public WifiVoucherStatus Status { get; private set; }

        public Guid? AssignedOrderItemId { get; private set; }

        public DateTimeOffset? AssignedAt { get; private set; }

        public Guid? ImportedBy { get; private set; }

        public DateTimeOffset ImportedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public static WifiVoucher Create(
            Guid id,
            Guid packageId,
            string code,
            DateTimeOffset now,
            Guid? importedBy)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The WiFi voucher id is required.", nameof(id));
            }

            if (packageId == Guid.Empty)
            {
                throw new ArgumentException("The WiFi package id is required.", nameof(packageId));
            }

            return new WifiVoucher
            {
                Id = id,
                PackageId = packageId,
                Code = ValidateCode(code),
                Status = WifiVoucherStatus.Available,
                ImportedBy = importedBy,
                ImportedAt = now,
                UpdatedAt = now,
            };
        }

        public void Assign(Guid orderItemId, DateTimeOffset now)
        {
            if (Status != WifiVoucherStatus.Available)
            {
                throw new InvalidOperationException(
                    $"Only an available voucher can be assigned. The status is {Status}.");
            }

            if (orderItemId == Guid.Empty)
            {
                throw new ArgumentException("The order item id is required.", nameof(orderItemId));
            }

            Status = WifiVoucherStatus.Assigned;
            AssignedOrderItemId = orderItemId;
            AssignedAt = now;
            UpdatedAt = now;
        }

        public bool Void(DateTimeOffset now)
        {
            if (Status != WifiVoucherStatus.Available)
            {
                throw new InvalidOperationException($"Only an available voucher can be voided. The status is {Status}.");
            }

            Status = WifiVoucherStatus.Void;
            UpdatedAt = now;
            return true;
        }

        public static bool IsValidCode(string? code)
        {
            var trimmed = code?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length is >= MinimumCodeLength and <= MaximumCodeLength
                && trimmed.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
        }

        private static string ValidateCode(string code)
        {
            if (!IsValidCode(code))
            {
                throw new ArgumentException(
                    $"The WiFi voucher code must be {MinimumCodeLength} to {MaximumCodeLength} letters, digits, or hyphens.",
                    nameof(code));
            }

            return code.Trim();
        }
    }
}
