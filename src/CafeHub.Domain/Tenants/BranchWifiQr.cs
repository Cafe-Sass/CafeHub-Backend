namespace CafeHub.Domain.Tenants
{
    public sealed class BranchWifiQr
    {
        public const int CodeLength = 22;

        private BranchWifiQr()
        {
        }

        public Guid BranchId { get; private set; }

        public string QrCode { get; private set; } = string.Empty;

        public WifiQrStatus Status { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static BranchWifiQr Create(
            Guid branchId,
            string qrCode,
            DateTimeOffset now,
            Guid? actorId)
        {
            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            return new BranchWifiQr
            {
                BranchId = branchId,
                QrCode = ValidateCode(qrCode),
                Status = WifiQrStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        public void Regenerate(string qrCode, DateTimeOffset now, Guid? actorId)
        {
            if (Status != WifiQrStatus.Active)
            {
                throw new InvalidOperationException("An inactive WiFi QR cannot be regenerated.");
            }

            if (string.Equals(QrCode, qrCode, StringComparison.Ordinal))
            {
                throw new ArgumentException("The replacement WiFi QR code must be different.", nameof(qrCode));
            }

            QrCode = ValidateCode(qrCode);
            UpdatedAt = now;
            UpdatedBy = actorId;
        }

        public static bool IsValidCode(string? qrCode) =>
            qrCode is { Length: CodeLength }
            && qrCode.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

        private static string ValidateCode(string qrCode)
        {
            if (!IsValidCode(qrCode))
            {
                throw new ArgumentException(
                    "The WiFi QR code must be 22 URL-safe base64url characters.",
                    nameof(qrCode));
            }

            return qrCode;
        }
    }
}
