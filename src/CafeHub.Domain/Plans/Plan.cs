using System.Collections.ObjectModel;

namespace CafeHub.Domain.Plans
{
    public sealed class Plan
    {
        private readonly List<PlanVersion> versions;
        private readonly ReadOnlyCollection<PlanVersion> versionsView;

        private Plan()
        {
            versions = new List<PlanVersion>();
            versionsView = versions.AsReadOnly();
        }

        public Guid Id { get; private set; }

        public string Code { get; private set; } = string.Empty;

        public string DisplayName { get; private set; } = string.Empty;

        public bool IsSelectable { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public IReadOnlyCollection<PlanVersion> Versions => versionsView;

        public static Plan Create(
            Guid id,
            string code,
            string displayName,
            bool isSelectable,
            DateTimeOffset createdAt,
            Guid? createdBy = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The plan id is required.", nameof(id));
            }

            if (!IsWellFormedCode(code))
            {
                throw new ArgumentException(
                    "The plan code must start with a lowercase letter and contain only lowercase letters, digits, and hyphens, between 3 and 64 characters.",
                    nameof(code));
            }

            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 256)
            {
                throw new ArgumentException("The plan display name must be between 1 and 256 characters.", nameof(displayName));
            }

            return new Plan
            {
                Id = id,
                Code = code,
                DisplayName = displayName,
                IsSelectable = isSelectable,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            };
        }

        public PlanVersion AddVersion(
            Guid id,
            decimal price,
            string currencyCode,
            BillingPeriod billingPeriod,
            PlanVersionStatus status,
            IEnumerable<PlanEntitlement> entitlements,
            DateTimeOffset createdAt,
            Guid? createdBy = null)
        {
            var nextVersionNumber = versions.Count == 0 ? 1 : versions.Max(version => version.VersionNumber) + 1;

            var version = PlanVersion.Create(
                id,
                Id,
                nextVersionNumber,
                price,
                currencyCode,
                billingPeriod,
                status,
                entitlements,
                createdAt,
                createdBy);

            versions.Add(version);
            UpdatedAt = createdAt;
            UpdatedBy = createdBy;

            return version;
        }

        private static bool IsWellFormedCode(string? code)
        {
            if (code is null || code.Length is < 3 or > 64)
            {
                return false;
            }

            if (!char.IsBetween(code[0], 'a', 'z'))
            {
                return false;
            }

            return code.All(character =>
                char.IsBetween(character, 'a', 'z')
                || char.IsBetween(character, '0', '9')
                || character == '-');
        }
    }
}
