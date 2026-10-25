namespace CafeHub.Domain.Subscriptions
{
    public sealed class SubscriptionPeriod
    {
        private SubscriptionPeriod()
        {
        }

        public Guid Id { get; private set; }

        public Guid SubscriptionId { get; private set; }

        public Guid PlanVersionId { get; private set; }

        public int PeriodNumber { get; private set; }

        public SubscriptionPeriodStatus PeriodStatus { get; private set; }

        public decimal AgreedPrice { get; private set; }

        public string CurrencyCode { get; private set; } = string.Empty;

        public DateTimeOffset StartsAt { get; private set; }

        public DateTimeOffset? EndsAt { get; private set; }

        public Guid? RenewalRequestId { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static SubscriptionPeriod Create(
            Guid id,
            Guid subscriptionId,
            Guid planVersionId,
            int periodNumber,
            SubscriptionPeriodStatus periodStatus,
            decimal agreedPrice,
            string currencyCode,
            DateTimeOffset startsAt,
            DateTimeOffset? endsAt,
            Guid? renewalRequestId,
            DateTimeOffset createdAt,
            Guid? createdBy = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The subscription period id is required.", nameof(id));
            }

            if (subscriptionId == Guid.Empty)
            {
                throw new ArgumentException("The subscription id is required.", nameof(subscriptionId));
            }

            if (planVersionId == Guid.Empty)
            {
                throw new ArgumentException("The plan version id is required.", nameof(planVersionId));
            }

            if (periodNumber < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(periodNumber),
                    "The period number must be at least 1.");
            }

            if (!Enum.IsDefined(periodStatus))
            {
                throw new ArgumentException("The period status is not defined.", nameof(periodStatus));
            }

            if (agreedPrice < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(agreedPrice),
                    "The agreed price must be zero or greater.");
            }

            if (!IsWellFormedCurrencyCode(currencyCode))
            {
                throw new ArgumentException("The currency code must be three lowercase letters.", nameof(currencyCode));
            }

            if (endsAt is not null && endsAt <= startsAt)
            {
                throw new ArgumentException(
                    "The period end must be after the start.",
                    nameof(endsAt));
            }

            return new SubscriptionPeriod
            {
                Id = id,
                SubscriptionId = subscriptionId,
                PlanVersionId = planVersionId,
                PeriodNumber = periodNumber,
                PeriodStatus = periodStatus,
                AgreedPrice = agreedPrice,
                CurrencyCode = currencyCode,
                StartsAt = startsAt,
                EndsAt = endsAt,
                RenewalRequestId = renewalRequestId,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            };
        }

        private static bool IsWellFormedCurrencyCode(string? currencyCode)
        {
            return currencyCode is { Length: 3 }
                && currencyCode.All(character => char.IsBetween(character, 'a', 'z'));
        }
    }
}
