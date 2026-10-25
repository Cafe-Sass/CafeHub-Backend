using System.Collections.ObjectModel;

namespace CafeHub.Domain.Plans
{
    public sealed class PlanVersion
    {
        private const decimal MaximumPrice = 9_999_999_999.99m;

        private readonly List<PlanEntitlement> entitlements;
        private readonly ReadOnlyCollection<PlanEntitlement> entitlementsView;

        private PlanVersion()
        {
            entitlements = new List<PlanEntitlement>();
            entitlementsView = entitlements.AsReadOnly();
        }

        public Guid Id { get; private set; }

        public Guid PlanId { get; private set; }

        public int VersionNumber { get; private set; }

        public decimal Price { get; private set; }

        public string CurrencyCode { get; private set; } = string.Empty;

        public BillingPeriod BillingPeriod { get; private set; }

        public PlanVersionStatus Status { get; private set; }

        public IReadOnlyCollection<PlanEntitlement> Entitlements => entitlementsView;

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        internal static PlanVersion Create(
            Guid id,
            Guid planId,
            int versionNumber,
            decimal price,
            string currencyCode,
            BillingPeriod billingPeriod,
            PlanVersionStatus status,
            IEnumerable<PlanEntitlement> entitlements,
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

            ArgumentNullException.ThrowIfNull(entitlements);

            var entitlementRows = entitlements.ToArray();

            if (entitlementRows.Any(entitlement => entitlement.PlanVersionId != id))
            {
                throw new ArgumentException(
                    "Every entitlement must belong to the plan version that owns it.",
                    nameof(entitlements));
            }

            var hasDuplicateKeys = entitlementRows
                .GroupBy(entitlement => entitlement.Key, StringComparer.Ordinal)
                .Any(group => group.Count() > 1);

            if (hasDuplicateKeys)
            {
                throw new ArgumentException(
                    "An entitlement key must appear at most once per plan version.",
                    nameof(entitlements));
            }

            var version = new PlanVersion
            {
                Id = id,
                PlanId = planId,
                VersionNumber = versionNumber,
                Price = price,
                CurrencyCode = currencyCode,
                BillingPeriod = billingPeriod,
                Status = status,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            };

            version.entitlements.AddRange(entitlementRows);

            return version;
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
