namespace CafeHub.Domain.Subscriptions
{
    public sealed class Subscription
    {
        private Subscription()
        {
        }

        public Guid Id { get; private set; }

        public Guid? OnboardingRequestId { get; private set; }

        public Guid? TenantId { get; private set; }

        public SubscriptionStatus Status { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static Subscription Create(
            Guid id,
            Guid? onboardingRequestId,
            Guid? tenantId,
            SubscriptionStatus status,
            DateTimeOffset createdAt,
            Guid? createdBy = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The subscription id is required.", nameof(id));
            }

            if (!IsExactlyOneAnchorSet(onboardingRequestId, tenantId))
            {
                throw new ArgumentException(
                    "A subscription must be owned by exactly one of: an onboarding request or a tenant.",
                    nameof(onboardingRequestId));
            }

            if (!Enum.IsDefined(status))
            {
                throw new ArgumentException("The subscription status is not defined.", nameof(status));
            }

            return new Subscription
            {
                Id = id,
                OnboardingRequestId = onboardingRequestId,
                TenantId = tenantId,
                Status = status,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            };
        }

        private static bool IsExactlyOneAnchorSet(Guid? onboardingRequestId, Guid? tenantId)
        {
            var count = 0;
            if (onboardingRequestId is not null && onboardingRequestId != Guid.Empty)
            {
                count++;
            }

            if (tenantId is not null && tenantId != Guid.Empty)
            {
                count++;
            }

            return count == 1;
        }
    }
}
