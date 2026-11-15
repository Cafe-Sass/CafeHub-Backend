namespace CafeHub.Domain.Tenants
{
    public sealed class BranchTable
    {
        public const int MinimumNumber = 1;
        public const int MaximumNumber = 9999;
        public const int MaximumLabelLength = 64;
        public const int QrCodeLength = 22;

        private BranchTable()
        {
        }

        public Guid Id { get; private set; }

        public Guid BranchId { get; private set; }

        public int Number { get; private set; }

        public string? Label { get; private set; }

        public string QrCode { get; private set; } = string.Empty;

        public BranchTableStatus Status { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static BranchTable Create(
            Guid id,
            Guid branchId,
            int number,
            string? label,
            string qrCode,
            DateTimeOffset createdAt,
            Guid? createdBy = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The table id is required.", nameof(id));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            return new BranchTable
            {
                Id = id,
                BranchId = branchId,
                Number = ValidateNumber(number),
                Label = ValidateLabel(label),
                QrCode = ValidateQrCode(qrCode),
                Status = BranchTableStatus.Active,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            };
        }

        public void UpdateLabel(string? label, DateTimeOffset updatedAt, Guid? updatedBy)
        {
            Label = ValidateLabel(label);
            UpdatedAt = updatedAt;
            UpdatedBy = updatedBy;
        }

        public void Deactivate(DateTimeOffset updatedAt, Guid? updatedBy)
        {
            Status = BranchTableStatus.Inactive;
            UpdatedAt = updatedAt;
            UpdatedBy = updatedBy;
        }

        public void Reactivate(DateTimeOffset updatedAt, Guid? updatedBy)
        {
            Status = BranchTableStatus.Active;
            UpdatedAt = updatedAt;
            UpdatedBy = updatedBy;
        }

        private static int ValidateNumber(int number)
        {
            if (number < MinimumNumber || number > MaximumNumber)
            {
                throw new ArgumentException(
                    "The table number must be between 1 and 9999.",
                    nameof(number));
            }

            return number;
        }

        private static string? ValidateLabel(string? label)
        {
            var trimmed = label?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }

            if (trimmed.Length > MaximumLabelLength)
            {
                throw new ArgumentException(
                    "The table label must be at most 64 characters after trimming.",
                    nameof(label));
            }

            if (trimmed.Any(char.IsControl))
            {
                throw new ArgumentException("The table label must not contain control characters.", nameof(label));
            }

            return trimmed;
        }

        private static string ValidateQrCode(string qrCode)
        {
            if (qrCode is null
                || qrCode.Length != QrCodeLength
                || !qrCode.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
            {
                throw new ArgumentException(
                    "The table QR code must be 22 URL-safe base64url characters.",
                    nameof(qrCode));
            }

            return qrCode;
        }
    }
}
