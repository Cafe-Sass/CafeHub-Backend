namespace CafeHub.Domain.Subscriptions
{
    public sealed class SubscriptionHistory
    {
        public const int MaximumActorReferenceLength = 256;
        public const int MaximumReasonLength = 256;

        private SubscriptionHistory()
        {
        }

        public Guid Id { get; private set; }

        public Guid SubscriptionId { get; private set; }

        public SubscriptionStatus? FromStatus { get; private set; }

        public SubscriptionStatus ToStatus { get; private set; }

        public Guid? PlanVersionId { get; private set; }

        public int? PeriodNumber { get; private set; }

        public Guid? ActorPlatformAdminId { get; private set; }

        public string? ActorReference { get; private set; }

        public string? Reason { get; private set; }

        public DateTimeOffset OccurredAt { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public static SubscriptionHistory Create(
            Guid id,
            Guid subscriptionId,
            SubscriptionStatus? fromStatus,
            SubscriptionStatus toStatus,
            Guid? planVersionId,
            int? periodNumber,
            Guid? actorPlatformAdminId,
            string? actorReference,
            string? reason,
            DateTimeOffset occurredAt)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The subscription history id is required.", nameof(id));
            }

            if (subscriptionId == Guid.Empty)
            {
                throw new ArgumentException("The subscription id is required.", nameof(subscriptionId));
            }

            if (fromStatus is { } previous && !Enum.IsDefined(previous))
            {
                throw new ArgumentException("The previous subscription status is not defined.", nameof(fromStatus));
            }

            if (!Enum.IsDefined(toStatus))
            {
                throw new ArgumentException("The subscription status is not defined.", nameof(toStatus));
            }

            if (fromStatus == toStatus)
            {
                throw new ArgumentException(
                    "A subscription history row must describe a state change.", nameof(toStatus));
            }

            if (planVersionId == Guid.Empty)
            {
                throw new ArgumentException("The plan version id cannot be empty.", nameof(planVersionId));
            }

            if (periodNumber is < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(periodNumber), "The period number must be at least 1.");
            }

            if (actorPlatformAdminId == Guid.Empty)
            {
                throw new ArgumentException(
                    "The actor platform admin id cannot be empty.", nameof(actorPlatformAdminId));
            }

            return new SubscriptionHistory
            {
                Id = id,
                SubscriptionId = subscriptionId,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                PlanVersionId = planVersionId,
                PeriodNumber = periodNumber,
                ActorPlatformAdminId = actorPlatformAdminId,
                ActorReference = Normalize(actorReference, MaximumActorReferenceLength, nameof(actorReference)),
                Reason = Normalize(reason, MaximumReasonLength, nameof(reason)),
                OccurredAt = occurredAt,
                CreatedAt = occurredAt,
            };
        }

        private static string? Normalize(string? value, int maximumLength, string parameterName)
        {
            if (value is null)
            {
                return null;
            }

            var trimmed = value.Trim();
            if (trimmed.Length == 0 || trimmed.Length > maximumLength || trimmed.Any(char.IsControl))
            {
                throw new ArgumentException(
                    $"The value must be 1 to {maximumLength} characters without control characters.",
                    parameterName);
            }

            return trimmed;
        }
    }
}
