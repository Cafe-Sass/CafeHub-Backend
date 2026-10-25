namespace CafeHub.Domain.Plans
{
    public sealed class PlanVersion
    {
        private const decimal MaximumPrice = 9_999_999_999.99m;

        private PlanVersion(
            Guid id,
            Guid planId,
            int versionNumber,
            decimal price,
            string currencyCode,
            BillingPeriod billingPeriod,
            PlanVersionStatus status,
            DateTimeOffset createdAt,
            Guid? createdBy)
        {
            Id = id;
            PlanId = planId;
            VersionNumber = versionNumber;
            Price = price;
            CurrencyCode = currencyCode;
            BillingPeriod = billingPeriod;
            Status = status;
            CreatedAt = createdAt;
            UpdatedAt = createdAt;
            CreatedBy = createdBy;
            UpdatedBy = createdBy;
        }

        public Guid Id { get; }

        public Guid PlanId { get; }

        public int VersionNumber { get; }

        public decimal Price { get; }

        public string CurrencyCode { get; } = string.Empty;

        public BillingPeriod BillingPeriod { get; }

        public PlanVersionStatus Status { get; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset UpdatedAt { get; }

        public Guid? CreatedBy { get; }

        public Guid? UpdatedBy { get; }

        internal static PlanVersion Create(
            Guid id,
            Guid planId,
            int versionNumber,
            decimal price,
            string currencyCode,
            BillingPeriod billingPeriod,
            PlanVersionStatus status,
            DateTimeOffset createdAt,
            Guid? createdBy)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The plan version id is required.", nameof(id));
            }

            if (planId == Guid.Empty)
            {
                throw new ArgumentException("The plan id is required.", nameof(planId));
            }

            if (versionNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(versionNumber), "The plan version number must be at least 1.");
            }

            if (!IsWellFormedPrice(price))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(price),
                    "The plan version price must be between 0 and 9,999,999,999.99 with at most two decimal places.");
            }

            if (!IsWellFormedCurrencyCode(currencyCode))
            {
                throw new ArgumentException("The currency code must be three lowercase letters.", nameof(currencyCode));
            }

            if (!Enum.IsDefined(billingPeriod))
            {
                throw new ArgumentException("The billing period is not defined.", nameof(billingPeriod));
            }

            if (!Enum.IsDefined(status))
            {
                throw new ArgumentException("The plan version status is not defined.", nameof(status));
            }

            return new PlanVersion(
                id,
                planId,
                versionNumber,
                price,
                currencyCode,
                billingPeriod,
                status,
                createdAt,
                createdBy);
        }

        private static bool IsWellFormedPrice(decimal price)
        {
            if (price < 0m || price > MaximumPrice)
            {
                return false;
            }

            var scale = (decimal.GetBits(price)[3] >> 16) & 0xFF;
            return scale <= 2;
        }

        private static bool IsWellFormedCurrencyCode(string? currencyCode)
        {
            return currencyCode is { Length: 3 }
                && currencyCode.All(character => char.IsBetween(character, 'a', 'z'));
        }
    }
}
