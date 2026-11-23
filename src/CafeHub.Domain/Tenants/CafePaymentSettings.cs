namespace CafeHub.Domain.Tenants
{
    public sealed class CafePaymentSettings
    {
        public const string DefaultMethodLabel = "InstaPay";
        public const int MaximumMethodLabelLength = 32;
        public const int MaximumPaymentLinkLength = 512;
        public const int MaximumPaymentAddressLength = 128;
        public const int MaximumDisplayNameLength = 128;

        private CafePaymentSettings()
        {
        }

        public Guid Id { get; private set; }

        public bool OnlineEnabled { get; private set; }

        public string MethodLabel { get; private set; } = DefaultMethodLabel;

        public string? PaymentLink { get; private set; }

        public string? PaymentAddress { get; private set; }

        public string? DisplayName { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static CafePaymentSettings Create(
            Guid id,
            DateTimeOffset createdAt,
            Guid? createdBy = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The payment settings id is required.", nameof(id));
            }

            return new CafePaymentSettings
            {
                Id = id,
                OnlineEnabled = false,
                MethodLabel = DefaultMethodLabel,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            };
        }

        public void Update(
            bool onlineEnabled,
            string? methodLabel,
            string? paymentLink,
            string? paymentAddress,
            string? displayName,
            DateTimeOffset updatedAt,
            Guid? updatedBy)
        {
            var trimmedMethodLabel = NormalizeOptional(methodLabel) ?? DefaultMethodLabel;
            var trimmedLink = NormalizeOptional(paymentLink);
            var trimmedAddress = NormalizeOptional(paymentAddress);
            var trimmedDisplayName = NormalizeOptional(displayName);

            if (onlineEnabled && trimmedLink is null && trimmedAddress is null)
            {
                throw new ArgumentException(
                    "Enabling online payment requires a payment link or a payment address.",
                    nameof(onlineEnabled));
            }

            if (trimmedMethodLabel.Length is < 1 or > MaximumMethodLabelLength
                || HasControlCharacters(trimmedMethodLabel))
            {
                throw new ArgumentException(
                    $"The method label must be 1 to {MaximumMethodLabelLength} characters without control characters.",
                    nameof(methodLabel));
            }

            if (trimmedLink is not null && !IsValidPaymentLink(trimmedLink))
            {
                throw new ArgumentException(
                    "The payment link must be an absolute https URL of up to 512 characters.",
                    nameof(paymentLink));
            }

            if (trimmedAddress is not null
                && (trimmedAddress.Length is < 1 or > MaximumPaymentAddressLength
                    || HasControlCharacters(trimmedAddress)))
            {
                throw new ArgumentException(
                    $"The payment address must be 1 to {MaximumPaymentAddressLength} characters without control characters.",
                    nameof(paymentAddress));
            }

            if (trimmedDisplayName is not null
                && (trimmedDisplayName.Length is < 1 or > MaximumDisplayNameLength
                    || HasControlCharacters(trimmedDisplayName)))
            {
                throw new ArgumentException(
                    $"The display name must be 1 to {MaximumDisplayNameLength} characters without control characters.",
                    nameof(displayName));
            }

            OnlineEnabled = onlineEnabled;
            MethodLabel = trimmedMethodLabel;
            PaymentLink = trimmedLink;
            PaymentAddress = trimmedAddress;
            DisplayName = trimmedDisplayName;
            UpdatedAt = updatedAt;
            UpdatedBy = updatedBy;
        }

        public static bool IsValidPaymentLink(string? paymentLink)
        {
            var trimmed = NormalizeOptional(paymentLink);
            if (trimmed is null)
            {
                return false;
            }

            return trimmed.Length <= MaximumPaymentLinkLength
                && Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps;
        }

        private static string? NormalizeOptional(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static bool HasControlCharacters(string value) =>
            value.Any(char.IsControl);
    }
}
