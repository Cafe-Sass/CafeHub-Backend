namespace CafeHub.Domain.Subscriptions
{
    public sealed class PlatformPaymentTransaction
    {
        private PlatformPaymentTransaction()
        {
        }

        public Guid Id { get; private set; }

        public Guid? OnboardingRequestId { get; private set; }

        public Guid? RenewalRequestId { get; private set; }

        public string IdempotencyKey { get; private set; } = string.Empty;

        public decimal Amount { get; private set; }

        public string CurrencyCode { get; private set; } = string.Empty;

        public PaymentTransactionStatus Status { get; private set; }

        public DateTimeOffset? SettledAt { get; private set; }

        public string? FailureCode { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static PlatformPaymentTransaction Create(
            Guid id,
            Guid? onboardingRequestId,
            Guid? renewalRequestId,
            string idempotencyKey,
            decimal amount,
            string currencyCode,
            PaymentTransactionStatus status,
            DateTimeOffset? settledAt,
            string? failureCode,
            DateTimeOffset createdAt,
            Guid? createdBy = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The payment transaction id is required.", nameof(id));
            }

            if (!IsExactlyOneTargetSet(onboardingRequestId, renewalRequestId))
            {
                throw new ArgumentException(
                    "A payment transaction must be attached to exactly one of: an onboarding request or a renewal request.",
                    nameof(onboardingRequestId));
            }

            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length < 8 || idempotencyKey.Length > 128)
            {
                throw new ArgumentException(
                    "The idempotency key must be between 8 and 128 characters.",
                    nameof(idempotencyKey));
            }

            if (amount < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    "The amount must be zero or greater.");
            }

            if (!IsWellFormedCurrencyCode(currencyCode))
            {
                throw new ArgumentException("The currency code must be three lowercase letters.", nameof(currencyCode));
            }

            if (!Enum.IsDefined(status))
            {
                throw new ArgumentException("The payment transaction status is not defined.", nameof(status));
            }

            if (failureCode is not null && failureCode.Length > 128)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(failureCode),
                    "The failure code must not exceed 128 characters.");
            }

            return new PlatformPaymentTransaction
            {
                Id = id,
                OnboardingRequestId = onboardingRequestId,
                RenewalRequestId = renewalRequestId,
                IdempotencyKey = idempotencyKey,
                Amount = amount,
                CurrencyCode = currencyCode,
                Status = status,
                SettledAt = settledAt,
                FailureCode = failureCode,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            };
        }

        private static bool IsExactlyOneTargetSet(Guid? onboardingRequestId, Guid? renewalRequestId)
        {
            var count = 0;
            if (onboardingRequestId is not null && onboardingRequestId != Guid.Empty)
            {
                count++;
            }

            if (renewalRequestId is not null && renewalRequestId != Guid.Empty)
            {
                count++;
            }

            return count == 1;
        }

        private static bool IsWellFormedCurrencyCode(string? currencyCode)
        {
            return currencyCode is { Length: 3 }
                && currencyCode.All(character => char.IsBetween(character, 'a', 'z'));
        }
    }
}
